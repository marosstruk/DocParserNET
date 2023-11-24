using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using DocParser;

namespace DocParserAPI.Models
{
    public class Doc
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Filename { get; set; } = null!;

        public string File { get; set; } = null!;

        public DateTime Inserted { get; set; } = DateTime.Now;

        public IDocData Data { get; set; } = null!;
    }
}
