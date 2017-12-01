using Newtonsoft.Json;

namespace Appy.GitDb.Core.Model
{
    public class Document : Document<string>
    {
        public static Document From<T>(Document<T> source) =>
            new Document {Key = source.Key, Value = JsonConvert.SerializeObject(source.Value, Formatting.Indented)};
    }
    public class Document<T>
    {
        public string Key { get; set; }
        public T Value { get; set; }
    }
}