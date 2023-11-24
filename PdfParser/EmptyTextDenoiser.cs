namespace DocParser
{
    public class EmptyTextDenoiser : ITextDenoiser
    {
        public string RemoveNoise(string text)
        {
            return text;
        }

        public Task<string> RemoveNoiseAsync(string text)
        {
            return new Task<string>(() => text);
        }
    }
}
