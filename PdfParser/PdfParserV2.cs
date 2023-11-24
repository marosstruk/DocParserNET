using PDFtoImage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace DocParser
{
    public class PdfParserV2 : IDocParser
    {
        private const string DEFAULT_TESS_DATA = @"./tessdata";
        private readonly string _tessData;

        public ITextDenoiser Denoiser { get; }
        public ITextCorrector Corrector { get; }


        public PdfParserV2(ITextDenoiser? denoiser, ITextCorrector? corrector)
            : this(DEFAULT_TESS_DATA, denoiser, corrector)
        {
        }

        public PdfParserV2(string tessData = DEFAULT_TESS_DATA,
            ITextDenoiser? denoiser = null, ITextCorrector? corrector = null)
        {
            this._tessData = tessData;
            this.Denoiser = denoiser ?? new EmptyTextDenoiser();
            this.Corrector = corrector ?? new EmptyTextCorrector();
        }

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

            processPage(bitmapPages[10], 11);

            return null;
        }

        private void processPage(SKBitmap bitmap, int pageNum)
        {
            var data = SKImage.FromBitmap(bitmap).Encode().ToArray();

            using var engine = new TesseractEngine(_tessData, "eng", EngineMode.Default);
            using var pageImage = Pix.LoadFromMemory(data);
            using var page = engine.Process(pageImage, PageSegMode.SingleColumn);

            var test3 = page.GetBoxText(pageNum);

            var iter = page.GetIterator();

            var test1 = page.GetLSTMBoxText(pageNum);
            var test2 = page.GetTsvText(pageNum);
            
            var test4 = page.GetWordStrBoxText(pageNum);
            var test5 = page.AnalyseLayout();

            

            //string pageText = page.GetText();
            //pageText = this.Denoiser.RemoveNoise(pageText);
            //pageText = this.Corrector.Correct(pageText);

            do
            {
                if (iter.BlockType == PolyBlockType.Table)
                {
                    string blockText = iter.GetText(PageIteratorLevel.Block);

                    blockText = this.Denoiser.RemoveNoise(blockText);
                    blockText = this.Corrector.Correct(blockText);


                }
            } while (iter.Next(PageIteratorLevel.Block));
        }

        private void parseTable(string blockText)
        {

        }
    }
}
