using DocParser.ExtensionMethods;
using PDFtoImage;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Tesseract;
using Tesseract.Interop;

namespace DocParser
{
    public class PdfParser : IDocParser
    {
        private readonly struct Symbol
        {
            public readonly string v;
            public readonly float x;
            public readonly float y;
            public readonly Rect box;

            public Symbol(string v, float x, float y, Rect box)
            {
                this.v = v;
                this.x = x;
                this.y = y;
                this.box = box;
            }
        }

        private class Cell : IComparable<Cell>, IEquatable<Cell>
        {
            public int X1 { get; init; }
            public int Y1 { get; init; }
            public int X2 { get; init; }
            public int Y2 { get; init; }
            public string Value { get; private set; } = string.Empty;

            public void Append(char c)
            {
                Value += c;
            }

            public void Append(string s)
            {
                Value += s;
            }

            public bool Contains(int x, int y)
            {
                return x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
            }

            public bool Contains(float x, float y)
            {
                return x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
            }

            public int CompareTo(Cell? other)
            {
                if (other == null)
                    throw new ArgumentNullException(nameof(other));

                return (X1 * X1 + Y1 * Y1).CompareTo(other.X1 * other.X1 + other.Y1 * other.Y1)
                    + (X2 * X2 + Y2 * Y2).CompareTo(other.X2 * other.X2 + other.Y2 * other.Y2);
            }

            public bool Equals(Cell? other)
            {
                if (other == null)
                    throw new ArgumentNullException(nameof(other));

                return this.CompareTo(other) == 0;
            }
        }

        private readonly string _tessData;

        private volatile string _company;
        private volatile string _registeredNumber;
        private volatile string _reportingPeriod;
        private ConcurrentBag<Page> _pages = new();
        private ConcurrentBag<LabeledTable> _structuredData = new();

        //public string[] StatementNames = new[] { "STATEMENT OF CHANGES IN EQUITY", };

        public PdfParser(string tessData = @"./tessdata")
        {
            _tessData = tessData;
        }

        #region Public Methods
        public IDocData Parse(string documentPath)
        {
            using var docStream = new FileStream(documentPath, FileMode.Open);
            return this.Parse(docStream);
        }

        public IDocData Parse(byte[] document)
        {
            using var docStream = new MemoryStream(document);
            return this.Parse(docStream);
        }

        public IDocData Parse(Stream document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var bitmapPages = Conversion.ToImages(document).ToArray();

            //int page = 4;
            //this.processPageV2(bitmapPages[page - 1], page);
            //using var file = File.OpenWrite($"../page{page}.png");
            //SKImage.FromBitmap(bitmapPages[page - 1]).Encode().SaveTo(file);

            //Parallel.For(0, bitmapPages.Length, (i, _) =>
            //{
            //    var bitmapPage = bitmapPages[i];
            //    this.processPageV2(bitmapPage, i + 1);
            //    using var file = File.OpenWrite($"../page{i + 1}.png");
            //    SKImage.FromBitmap(bitmapPages[i]).Encode().SaveTo(file);
            //    // Tesseract doesn't like to clean up after itself, so we gotta do it manually
            //    GC.Collect();
            //});

            Parallel.For(0, bitmapPages.Length, (i, _) =>
            {
                var bitmapPage = bitmapPages[i];
                this.processPage(bitmapPage, i + 1);
                bitmapPage.Dispose();
                // Tesseract doesn't like to clean up after itself, so we gotta do it manually
                GC.Collect();
            });

            var resultData = new DocData(
                bitmapPages.Length,
                this._company,
                this._registeredNumber,
                this._reportingPeriod,
                this._pages.OrderBy(p => p.Num).ToList(),
                this._structuredData.OrderBy(d => d.PageNum).ToList());

            return resultData;
        }
        #endregion

        #region Private Methods
        private void processPageV2(SKBitmap bitmap, int pageNum)
        {
            var data = SKImage.FromBitmap(bitmap).Encode().ToArray();

            using var engine = new TesseractEngine(_tessData, "eng", EngineMode.Default);
            using var pageImage = Pix.LoadFromMemory(data);
            using var page = engine.Process(pageImage, PageSegMode.SingleColumn);
            using var iter = page.GetIterator();

            string pageText = page.GetText();
            getSummaryDataPoints(pageText);

            this._pages.Add(new Page(pageNum, pageText));
            iter.Begin();

            var textLines = new Stack<List<Symbol>>();
            var tableSymbols = new List<Symbol>();
            List<(int minX, int maxX)>? cols = null;
            List<(int minY, int maxY)>? rows = null;

            do
            {
                do
                {
                    var textLine = new List<Symbol>();
                    do
                    {
                        do
                        {
                            textLine.Add(this.getSymbolObj(iter));

                        } while (iter.Next(PageIteratorLevel.Word, PageIteratorLevel.Symbol));

                        this.appendEmptySpacer(textLine);

                    } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                    if (iter.BlockType != PolyBlockType.Table)
                    {
                        textLine = this.eliminateNoise(textLine);
                        textLines.Push(textLine);

                        if (cols != null && rows != null)
                        {
                            var newSymbolList = tableSymbols.Concat(textLine).ToList();
                            var newCols = this.identifyColumns(newSymbolList.Select(s => s.box).ToList());
                            if (cols.Count == newCols.Count)
                            {
                                tableSymbols = newSymbolList;
                                cols = newCols;
                            }
                            else
                            {
                                rows = this.identifyRows(tableSymbols.Select(s => s.box).ToList());
                                this.processTableV2(cols, rows, tableSymbols, pageNum, "", bitmap);
                                cols = null;
                                rows = null;
                                tableSymbols.Clear();
                            }
                        }
                    }
                    else
                    {
                        tableSymbols.AddRange(this.eliminateNoise(textLine));
                    }
                } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.TextLine));

                if (iter.BlockType == PolyBlockType.Table)
                {
                    tableSymbols = this.eliminateNoise(tableSymbols);
                    cols = this.identifyColumns(tableSymbols.Select(s => s.box).ToList());

                    foreach (var textLine in textLines)
                    {
                        var newSymbolList = textLine.Concat(tableSymbols).ToList();
                        var newCols = this.identifyColumns(newSymbolList.Select(s => s.box).ToList());
                        if (cols.Count == newCols.Count)
                        {
                            tableSymbols = newSymbolList;
                            cols = newCols;
                        }
                        else
                        {
                            break;
                        }
                    }
                    textLines.Clear();

                    rows = this.identifyRows(tableSymbols.Select(s => s.box).ToList());

                    //this.getExtremes(cols, out int minX, out int maxX);
                    //this.getExtremes(rows, out int minY, out int maxY);

                    //foreach (var col in cols)
                    //    this.drawOnBitmap(bitmap, new Rect(
                    //        col.minX, minY, col.maxX - col.minX, maxY - minY), SKColors.Blue, 3);
                    //foreach (var row in rows)
                    //    this.drawOnBitmap(bitmap, new Rect(
                    //        minX, row.minY, maxX - minX, row.maxY - row.minY), SKColors.Green, 3);
                }
            } while (iter.Next(PageIteratorLevel.Block));

            if (cols != null && rows != null)
            {
                this.processTableV2(cols, rows, tableSymbols, pageNum, "", bitmap);
            }
        }

        private Symbol getSymbolObj(ResultIterator iter)
        {
            iter.TryGetBoundingBox(PageIteratorLevel.Symbol, out Rect symbolBox);
            string symbolValue = iter.GetText(PageIteratorLevel.Symbol);
            float x = symbolBox.GetCentroid().X;
            float y = symbolBox.GetCentroid().Y;
            return new Symbol(symbolValue, x, y, symbolBox);
        }

        private void appendEmptySpacer(List<Symbol> symbols)
        {
            var ls = symbols.Last();
            symbols.Add(new Symbol(" ", ls.x, ls.y, ls.box));
        }

        private void processPage(SKBitmap bitmap, int pageNum)
        {
            var data = SKImage.FromBitmap(bitmap).Encode().ToArray();

            using var engine = new TesseractEngine(_tessData, "eng", EngineMode.Default);
            using var pageImage = Pix.LoadFromMemory(data);
            using var page = engine.Process(pageImage, PageSegMode.SingleColumn);
            using var iter = page.GetIterator();

            string pageText = page.GetText();
            string pageTextBoxes = page.GetLSTMBoxText(pageNum);
            getSummaryDataPoints(pageText);

            this._pages.Add(new Page(pageNum, pageText));
            iter.Begin();

            do
            {
                // Used to determine columns and rows
                var symbolBoxes = new List<Rect>();
                var symbols = new List<List<(string, float)>>();
                var symbolsV2 = new List<Symbol>();

                if (iter.BlockType == PolyBlockType.Table)
                {
                    do
                    {
                        do
                        {
                            var currLine = new List<(string, float)>();
                            // Used only for drawing
                            var currSymbols = new List<(string, Rect)>();

                            do
                            {
                                do
                                {
                                    iter.TryGetBoundingBox(PageIteratorLevel.Symbol, out Rect symbolBox);
                                    symbolBoxes.Add(symbolBox);

                                    string symbolValue = iter.GetText(PageIteratorLevel.Symbol);
                                    currLine.Add(new(symbolValue, symbolBox.GetCentroid().X));

                                    currSymbols.Add(new(symbolValue, symbolBox));

                                    float x = symbolBox.GetCentroid().X;
                                    float y = symbolBox.GetCentroid().Y;
                                    symbolsV2.Add(new Symbol(symbolValue, x, y, symbolBox));

                                } while (iter.Next(PageIteratorLevel.Word, PageIteratorLevel.Symbol));

                                var (X, Y) = symbolBoxes.Last().GetCentroid();
                                currLine.Add(new(" ", X));
                                symbolsV2.Add(new Symbol(" ", X, Y, symbolBoxes.Last()));

                            } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                            // For best results, need to go symbol by symbol, not word by word
                            symbols.Add(currLine);

                            currSymbols = eliminateNoise(currSymbols);

                            foreach (var symbol in currSymbols)
                            {
                                //this.drawOnBitmap(bitmap, symbol.Item2, SKColors.Blue, 3);
                            }

                        } while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
                    } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));

                    var cols = this.identifyColumns(symbolBoxes);
                    var rows = this.identifyRows(symbolBoxes);
                    this.getExtremes(cols, out int minX, out int maxX);
                    this.getExtremes(rows, out int minY, out int maxY);

                    //foreach (var col in cols)
                    //    this.drawOnBitmap(bitmap, new Rect(
                    //        col.minX, minY, col.maxX - col.minX, maxY - minY), SKColors.Blue, 3);
                    //foreach (var row in rows)
                    //    this.drawOnBitmap(bitmap, new Rect(
                    //        minX, row.minY, maxX - minX, row.maxY - row.minY), SKColors.Green, 3);

                    var tableName = this.getTableName(pageText);
                    var table = this.processTable(cols, symbols, pageNum, tableName);
                    this.processTableV2(cols, rows, symbolsV2, pageNum, tableName, bitmap);
                    this._structuredData.Add(table);
                }
            } while (iter.Next(PageIteratorLevel.Block));
        }

        private void getSummaryDataPoints(string pageText)
        {
            var statementNames = new[] { "STATEMENT OF COMPREHENSIVE INCOME", "Balance sheet", "Income Statement" };
            if (string.IsNullOrEmpty(this._company) && statementNames.Any(pageText.Contains))
            {
                this._company = pageText.Split("\n")[0];
            }

            if (string.IsNullOrEmpty(this._registeredNumber))
            {
                string pattern = @"(Registered Number|Registration No|Registered No)([\.:]*)(\d+)";
                this._registeredNumber = Regex
                    .Match(pageText, pattern, RegexOptions.IgnoreCase)
                    .Groups[3].Value.Trim();
            }

            if (string.IsNullOrEmpty(this._reportingPeriod))
            {
                string pattern = @"(FOR THE YEAR ENDED|For the period ended)(.+)";
                this._reportingPeriod = Regex
                    .Match(pageText, pattern, RegexOptions.IgnoreCase)
                    .Groups[2].Value.Trim();
            }
        }

        private bool getExtremes(List<(int, int)> values, out int min, out int max)
        {
            min = int.MaxValue;
            max = int.MinValue;

            if (values == null || values.Count == 0)
                return false;

            foreach (var val in values)
            {
                if (val.Item1 < min)
                    min = val.Item1;
                if (val.Item2 < min)
                    min = val.Item2;
                if (val.Item1 > max)
                    max = val.Item1;
                if (val.Item2 > max)
                    max = val.Item2;
            }

            return true;
        }

        private List<Symbol> eliminateNoise(List<Symbol> symbols)
        {
            return this.eliminateNoise(symbols.Select(s => (s.v, s.box)).ToList())
                .Select(x => new Symbol(x.Item1, x.Item2.GetCentroid().X, x.Item2.GetCentroid().Y, x.Item2))
                .ToList();
        }

        private List<(string, Rect)> eliminateNoise(List<(string value, Rect box)> symbols)
        {
            int eps = 50;
            var result = new List<(string, Rect)>();
            int count = symbols.Count;
            int last = count - 1;

            if (count < 3)
                return symbols;

            // Handle first symbol
            string v = symbols[0].value;
            float c = symbols[0].box.GetCentroid().X;
            float dRight = symbols[1].box.GetCentroid().X - c;
            if (!(Regex.IsMatch(v, @"[\D\W]") && (dRight > eps || dRight == 0)))
                result.Add(symbols[0]);

            // Handle last symbol
            v = symbols[last].value;
            c = symbols[last].box.GetCentroid().X;
            float dLeft = c - symbols[last - 1].box.GetCentroid().X;
            if (!(Regex.IsMatch(v, @"[\D\W]") && dLeft > eps))
                result.Add(symbols[last]);

            // Handle all other symbols in between
            for (int i = 1; i < symbols.Count - 1; i++)
            {
                v = symbols[i].value;
                c = symbols[i].box.GetCentroid().X;
                dLeft = c - symbols[i - 1].box.GetCentroid().X;
                dRight = symbols[i + 1].box.GetCentroid().X - c;

                if (Regex.IsMatch(v, @"[\D\W]") && dLeft > eps && (dRight > eps || dRight == 0))
                {
                    continue;
                }
                result.Add((v, symbols[i].box));
            }

            return result;
        }

        private LabeledTable processTableV2(List<(int, int)> cols, List<(int, int)> rows,
            List<Symbol> symbols, int pageNum, string tableName, SKBitmap bitmap)
        {
            var data = new string[rows.Count, cols.Count];
            var cells = new List<Cell>();

            foreach (var row in rows)
            {
                foreach (var col in cols)
                {
                    var cell = new Cell()
                    {
                        X1 = col.Item1,
                        Y1 = row.Item1,
                        X2 = col.Item2,
                        Y2 = row.Item2,
                    };
                    cells.Add(cell);
                    this.drawOnBitmap(bitmap, new Rect(
                        cell.X1, cell.Y1, cell.X2 - cell.X1, cell.Y2 - cell.Y1), SKColors.Blue, 3);
                }
            }
            
            foreach (var s in symbols)
            {
                this.drawOnBitmap(bitmap, s.box, SKColors.Red, 3);
                var cell = cells.FirstOrDefault(cell => cell.Contains(s.x, s.y));
                cell?.Append(s.v);
            }



            return null;
        }

        private LabeledTable processTable(List<(int, int)> cols, List<List<(string, float)>> symbols,
            int pageNum, string tableName)
        {
            var data = new string[symbols.Count, cols.Count];

            // Merge symbols into words
            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = 0; j < symbols[i].Count; j++)
                {
                    float x = symbols[i][j].Item2;
                    string v = symbols[i][j].Item1;

                    for (int k = 0; k < cols.Count; k++)
                    {
                        int x_min = cols[k].Item1;
                        int x_max = cols[k].Item2;

                        if (x >= x_min && x <= x_max)
                            data[i, k] = $"{data[i, k]}{v}";
                        else
                            data[i, k] = data[i, k] ?? "";
                    }
                }
            }

            data = this.mergeColumns(data);
            int rowCount = data.GetLength(0);
            int newColCount = data.GetLength(1);
            int startIdx = 0;

            // Get column headers
            var headers = new Dictionary<int, string>();
            for (int ri = 0; string.IsNullOrEmpty(data[ri, 0]); ri++)
            {
                for (int ci = 0; ci < newColCount; ci++)
                {
                    string potentialHeader = this.cleanData(data[ri, ci]);
                    if (Regex.IsMatch(potentialHeader, @"^Note$|^\d{4}$"))
                    {
                        headers.Add(ci, potentialHeader);
                    }
                }
                startIdx = ri + 1;
            }

            // Get row labels
            var labels = new Dictionary<int, string>();
            for (int ri = 0; ri < rowCount; ri++)
            {
                string potentialLabel = this.cleanData(data[ri, 0]);
                if (!string.IsNullOrEmpty(potentialLabel))
                {
                    labels.Add(ri, potentialLabel);
                }
            }

            var tableCols = new List<LabeledTableCol>();
            for (int ci = 1; ci < newColCount; ci++)
            {
                headers.TryGetValue(ci, out string header);
                if (header == "Note")
                    continue;
                var entries = new List<LabeledTableEntry>();

                if (tableName == "Statement Of Changes In Equity")
                    header = this.getChangesInEquityHeader(ci, newColCount);

                for (int ri = startIdx; ri < rowCount; ri++)
                {
                    labels.TryGetValue(ri, out string value);
                    entries.Add(new LabeledTableEntry(value, this.cleanData(data[ri, ci])));
                }
                tableCols.Add(new LabeledTableCol(header, entries));
            }

            return new LabeledTable(pageNum, tableName, tableCols);
        }

        private string getChangesInEquityHeader(int ci, int colCount)
        {
            string header = "";
            if (colCount == 4)
            {
                switch (ci)
                {
                    case 1:
                        header = "Share capital";
                        break;
                    case 2:
                        header = "Retained earnings";
                        break;
                    case 3:
                        header = "Total equity";
                        break;
                }
            }
            else
            {
                switch (ci)
                {
                    case 1:
                        header = "Share capital";
                        break;
                    case 2:
                        header = "Share premium";
                        break;
                    case 3:
                        header = "Retained earnings";
                        break;
                    case 4:
                        header = "Total equity";
                        break;
                }
            }
            return header;
        }

        private string getTableName(string pageText)
        {
            return Regex.Match(pageText, @"(Statement of [\w ]+|Balance Sheet)", RegexOptions.IgnoreCase)
                .Value.Trim().ToTitleCase();
        }

        private List<(int minX, int maxX)> identifyColumns(List<Rect> symbolBoxes)
        {
            var points = new List<int>();
            var avgWidths = new List<int>();
            foreach (var symbolBox in symbolBoxes)
            {
                //points.Add(symbolBox.X1);
                //points.Add(symbolBox.X2);
                points.Add(symbolBox.X1 + ((symbolBox.X2 - symbolBox.X1) / 2));
                avgWidths.Add(symbolBox.Width);
            }
            points = points.Distinct().ToList();
            points.Sort();

            int avgWidth = (int)avgWidths.Average();
            int eps = (int)(1.95 * avgWidth);

            var clusters = this.clusterPoints(points, eps);

            var cols = new List<(int, int)>();
            foreach (var c in clusters)
            {
                int minX = c.Min();
                int maxX = c.Max();
                if (maxX - minX > avgWidth)
                    cols.Add((minX - eps, maxX + eps));
            }
            return cols;
        }

        private List<(int minY, int maxY)> identifyRows(List<Rect> symbolBoxes)
        {
            var points = new List<int>();
            var avgHeights = new List<int>();
            foreach (var symbolBox in symbolBoxes)
            {
                points.Add(symbolBox.Y1 + ((symbolBox.Y2 - symbolBox.Y1) / 2));
                avgHeights.Add(symbolBox.Height);
            }
            points = points.Distinct().ToList();
            points.Sort();

            int avgHeight = (int)avgHeights.Average();
            int eps = (int)(1.5 * avgHeight);

            var clusters = this.clusterPoints(points, eps);

            var rows = new List<(int, int)>();
            foreach (var c in clusters)
            {
                int minY = c.Min();
                int maxY = c.Max();
                int diff = (int)(0.75 * avgHeight);
                rows.Add((minY - diff, maxY + diff));
            }
            return rows;
        }

        private List<List<int>> clusterPoints(List<int> points, int eps)
        {
            var clusters = new List<List<int>>();
            int curr_point = points[0];
            var curr_cluster = new List<int>() { curr_point };

            foreach (int p in points.Skip(1))
            {
                if (p <= curr_point + eps)
                {
                    curr_cluster.Add(p);
                }
                else
                {
                    clusters.Add(curr_cluster);
                    curr_cluster = new List<int>() { p };
                }
                curr_point = p;
            }
            clusters.Add(curr_cluster);

            return clusters;
        }

        private string[,] mergeColumns(string[,] data)
        {
            int rowCount = data.GetLength(0);
            int colCount = data.GetLength(1);
            var overlapScores = new int[colCount - 1];

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount - 1; j++)
                {
                    bool leftEmpty = string.IsNullOrEmpty(data[i, j]);
                    bool rightEmpty = string.IsNullOrEmpty(data[i, j + 1]);
                    if (!leftEmpty && !rightEmpty)
                    {
                        overlapScores[j]++;
                    }
                }
            }

            if (!overlapScores.Contains(0))
            {
                return data;
            }

            var mergedCols = new List<int>();

            for (int i = 0; i < colCount - 1; i++)
            {
                if (overlapScores[i] == 0 && !mergedCols.Contains(i))
                {
                    mergedCols.Add(i + 1);
                }
            }

            int resultColCount = colCount - mergedCols.Count;
            var result = new string[rowCount, resultColCount];

            for (int i = 0; i < rowCount; i++)
            {
                var newRow = new List<string>();
                for (int j = 0; j < colCount; j++)
                {
                    if (mergedCols.Contains(j + 1) && !string.IsNullOrEmpty(data[i, j + 1]))
                    {
                        newRow.Add(data[i, j + 1]);
                    }
                    else if (!mergedCols.Contains(j))
                    {
                        newRow.Add(data[i, j]);
                    }
                }
                for (int j = 0; j < resultColCount; j++)
                {
                    result[i, j] = newRow[j];
                }
            }

            return result;
        }

        private string cleanData(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return "";
            }

            var rgx = new Regex(@"[^\w\d,\. \(\)\\/]+");
            data = rgx.Replace(data, "").Trim();

            if (Regex.IsMatch(data, @"\([\d\.,]+\)"))
            {
                data = Regex.Replace(data, @"\(", "-");
                data = Regex.Replace(data, @"\)", "");
            }

            return data;
        }

        private void drawOnBitmap(SKBitmap bitmap, Rect rect, SKColor color, int boldness)
        {
            // Create canvas based on bitmapPage
            using (SKCanvas canvas = new SKCanvas(bitmap))
            {
                using (SKPaint paint = new SKPaint())
                {
                    paint.Style = SKPaintStyle.Stroke;
                    paint.Color = color;
                    paint.StrokeWidth = boldness;
                    paint.StrokeCap = SKStrokeCap.Butt;

                    canvas.DrawRect(new SKRect(rect.X1, rect.Y1, rect.X2, rect.Y2), paint);
                }
                canvas.Save();
            }
        }
        #endregion
    }
}