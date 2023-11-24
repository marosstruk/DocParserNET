// See https://aka.ms/new-console-template for more information
using DocParser;

//Console.WriteLine("Hello, World!");

var watch = new System.Diagnostics.Stopwatch();
watch.Start();

//string pdfPath = @"./pdftest.pdf";
string pdfPath = @"C:\Users\Maros\OneDrive\Work\Roussetos\Resources\Financial Statements\ARCHWOOD LIMITED 2017.pdf";
//var parser = new PdfParserV2(new RuleBasedTextDenoiser(), new GptTextCorrector());
var parser = new PdfParser();
var result = parser.Parse(pdfPath);

watch.Stop();

Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");


//const string text = @"Archwood Limited

//Statement of comprehensive income
//- For the petiod ended 30 September 2017

//2017 2016
//Note £000 £000
//Turnover ‘ | 4 32,551 41,000
//Cost of sales , : , (25,356) (32,083)
//Gtross profit _ 7,195 8,917
//Administrative expenses (9,797) ~ (9,408)
//Exceptional adrm'xﬁétrative expenses 12 (345) (1,135)
//Operating loss ' 5 (2,947) (1,626)
//Interest payable . ‘ 9 _ (10) ' 3
//Other finance expense E 26 (229) (226)
//Loss before tax . ' (3,186) . (1,855)
//Tax on loss before tax 11 145 249
//Loss for the financial period (3,041) (1,606)
//Other comprehensive income for the period
//Actuarial gain/(losses) on defined benefit pension scheme : 26 T 2,666 (3,918)
//Movement of deferred tax relating to pension surplus (237) 292
//Other comprehensive income for the period 2,429 (3,6206)
//Total comprehensive income for the period (612) (5:232)

//There were no recognised gains and losses for 2017 or 2016 other than those included in the statement of *
//comprehensive income. : :

//The notes on 'pagcs 11 to 31 form part of these financial statements.

//A Page 8";

//const string text1 = "Fort the period ended 30 September 2017";

//var textCorrector = new GptTextCorrector();

//string correctedText = await textCorrector.CorrectAsync(text);

//Console.WriteLine(correctedText);

