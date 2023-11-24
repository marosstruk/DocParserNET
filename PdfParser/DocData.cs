using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocParser
{
    public record class Page(int Num, string Text);

    public record class LabeledTable(int PageNum, string Name, List<LabeledTableCol> Columns);

    public record class LabeledTableCol(string Header, List<LabeledTableEntry> Entries);

    public record class LabeledTableEntry(string Label, string Value);

    public record class Cell(string Header, string Label, string Value,
        (int x1, int y1, int x2, int y2) Coords);

    public class DocData : IDocData
    {
        public int PageCount { get; set; }
        public string Company { get; set; }
        public string RegisteredNumber { get; set; }
        public string ReportingPeriod { get; set; }
        public List<Page> Pages { get; set; }
        public List<LabeledTable> StructuredData { get; set; }

        public DocData(int pageCount = 0, string company = "", string registeredNumber = "",
            string reportingPeriod = "", List<Page>? pages = null, List<LabeledTable>? structuredData = null)
        {
            this.PageCount = pageCount;
            this.Company = company;
            this.RegisteredNumber = registeredNumber;
            this.ReportingPeriod = reportingPeriod;
            this.Pages = pages ?? new List<Page>();
            this.StructuredData = structuredData ?? new List<LabeledTable>();
        }

        public string ToJson(bool prettyPrint = false)
        {
            var options = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = prettyPrint
            };
            var jsonString = JsonSerializer.Serialize(this, options);

            return jsonString;
        }
    }
}
