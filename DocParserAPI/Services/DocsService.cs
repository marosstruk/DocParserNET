using DocParserAPI.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DocParserAPI.Services
{
    public class DocsService
    {
        private readonly IMongoCollection<Doc> _docsCollection;

        public DocsService(IOptions<DocParserDatabaseSettings> bookStoreDatabaseSettings)
        {
            var mongoClient = new MongoClient(
                bookStoreDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                bookStoreDatabaseSettings.Value.DatabaseName);

            _docsCollection = mongoDatabase.GetCollection<Doc>(
                bookStoreDatabaseSettings.Value.BooksCollectionName);
        }

        public async Task<List<Doc>> GetAsync() =>
            await _docsCollection.Find(_ => true).ToListAsync();

        public async Task<Doc?> GetAsync(string id) =>
            await _docsCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(Doc newDoc) =>
            await _docsCollection.InsertOneAsync(newDoc);

        public async Task UpdateAsync(string id, Doc updatedDoc) =>
            await _docsCollection.ReplaceOneAsync(x => x.Id == id, updatedDoc);

        public async Task RemoveAsync(string id) =>
            await _docsCollection.DeleteOneAsync(x => x.Id == id);
    }
}
