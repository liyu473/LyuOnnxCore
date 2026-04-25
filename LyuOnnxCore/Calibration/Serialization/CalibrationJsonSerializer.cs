using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Serialization;

internal static class CalibrationJsonSerializer
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new OpenCvSizeJsonConverter());
        options.Converters.Add(new OpenCvPoint2dJsonConverter());
        options.Converters.Add(new OpenCvVec3dJsonConverter());

        return options;
    }

    private sealed class OpenCvSizeJsonConverter : JsonConverter<Size>
    {
        public override Size Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Size must be a JSON object.");
            }

            int width = 0;
            int height = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Size(width, height);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Invalid Size JSON.");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "Width":
                        width = reader.GetInt32();
                        break;
                    case "Height":
                        height = reader.GetInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Invalid Size JSON.");
        }

        public override void Write(Utf8JsonWriter writer, Size value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Width", value.Width);
            writer.WriteNumber("Height", value.Height);
            writer.WriteEndObject();
        }
    }

    private sealed class OpenCvPoint2dJsonConverter : JsonConverter<Point2d>
    {
        public override Point2d Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Point2d must be a JSON object.");
            }

            double x = 0;
            double y = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Point2d(x, y);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Invalid Point2d JSON.");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "X":
                        x = reader.GetDouble();
                        break;
                    case "Y":
                        y = reader.GetDouble();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Invalid Point2d JSON.");
        }

        public override void Write(Utf8JsonWriter writer, Point2d value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }

    private sealed class OpenCvVec3dJsonConverter : JsonConverter<Vec3d>
    {
        public override Vec3d Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Vec3d must be a JSON object.");
            }

            double x = 0;
            double y = 0;
            double z = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Vec3d(x, y, z);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Invalid Vec3d JSON.");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "X":
                        x = reader.GetDouble();
                        break;
                    case "Y":
                        y = reader.GetDouble();
                        break;
                    case "Z":
                        z = reader.GetDouble();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Invalid Vec3d JSON.");
        }

        public override void Write(Utf8JsonWriter writer, Vec3d value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.Item0);
            writer.WriteNumber("Y", value.Item1);
            writer.WriteNumber("Z", value.Item2);
            writer.WriteEndObject();
        }
    }
}
