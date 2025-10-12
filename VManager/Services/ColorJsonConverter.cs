using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace VManager.Services;

public class ColorJsonConverter : JsonConverter<Color?>
{
    public override Color? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        if (reader.TokenType == JsonTokenType.String)
        {
            var colorString = reader.GetString();
            if (string.IsNullOrEmpty(colorString))
                return null;
                
            return Color.Parse(colorString);
        }
        
        // Intentar leer como objeto con A,R,G,B
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            byte a = 255, r = 0, g = 0, b = 0;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                    
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();
                    
                    switch (propName)
                    {
                        case "A": a = reader.GetByte(); break;
                        case "R": r = reader.GetByte(); break;
                        case "G": g = reader.GetByte(); break;
                        case "B": b = reader.GetByte(); break;
                    }
                }
            }
            
            // Si todos son 0, retornar null
            if (a == 0 && r == 0 && g == 0 && b == 0)
                return null;
                
            return Color.FromArgb(a, r, g, b);
        }
        
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Color? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            // Guardar como string hex (más legible)
            writer.WriteStringValue(value.Value.ToString());
        }
    }
}