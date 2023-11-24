namespace DocParser
{
    public class EmptyTextCorrector : ITextCorrector
    {
        public string Correct(string text)
        {
            return text;
        }

        public Task<string> CorrectAsync(string text)
        {
            return new Task<string>(() => text);
        }
    }
}
