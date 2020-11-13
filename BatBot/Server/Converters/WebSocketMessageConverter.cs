﻿using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BatBot.Server.Converters
{
    public class BigIntegerConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue((ulong)value);
        }
    }
}
