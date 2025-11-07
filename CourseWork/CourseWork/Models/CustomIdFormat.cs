using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CourseWork.Models
{
    public enum CustomIdPartType
    {
        FixedText = 0,
        Random20Bit = 1,
        Random32Bit = 2,
        Random6Digit = 3,
        Random9Digit = 4,
        Guid = 5,
        DateTime = 6,
        Sequence = 7
    }

    public class CustomIdPart
    {
        public CustomIdPartType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty; 
    }

    public class CustomIdFormat
    {
        public List<CustomIdPart> Parts { get; set; } = new List<CustomIdPart>();

        public static CustomIdFormat? FromJson(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<CustomIdFormat>(json);
            }
            catch
            {
                return null;
            }
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public string GeneratePreview()
        {
            var parts = new List<string>();
            
            foreach (var part in Parts)
            {
                switch (part.Type)
                {
                    case CustomIdPartType.FixedText:
                        parts.Add(part.Value);
                        break;
                    case CustomIdPartType.Random20Bit:
                        parts.Add("ABC12");
                        break;
                    case CustomIdPartType.Random32Bit:
                        parts.Add("ABCD1234");
                        break;
                    case CustomIdPartType.Random6Digit:
                        parts.Add("123456");
                        break;
                    case CustomIdPartType.Random9Digit:
                        parts.Add("123456789");
                        break;
                    case CustomIdPartType.Guid:
                        parts.Add("a1b2c3d4");
                        break;
                    case CustomIdPartType.DateTime:
                        parts.Add("20250101");
                        break;
                    case CustomIdPartType.Sequence:
                        parts.Add("0001");
                        break;
                }
            }

            return string.Join("", parts);
        }
    }
}
