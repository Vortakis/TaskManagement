using Newtonsoft.Json;

namespace TaskManagement.Api.Common.Converters
{
    public class SingleLineListConverter<T> : JsonConverter<List<T>>
    {
        public override void WriteJson(JsonWriter writer, List<T>? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteRawValue(JsonConvert.SerializeObject(value, Formatting.None));
        }

        public override List<T>? ReadJson(JsonReader reader, Type objectType, List<T>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<List<T>>(reader);
        }

        public override bool CanRead => true;
    }
}
