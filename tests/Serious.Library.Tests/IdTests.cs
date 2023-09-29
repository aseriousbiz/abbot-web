using System;
using Newtonsoft.Json;
using Serious;
using Xunit;
using STJ = System.Text.Json;

public class IdTests
{
    public class TheEqualityComparer
    {
        [Fact]
        public void TreatsEquivalentIdsAsEqual()
        {
            var id1 = new Id<string>(1);
            var id2 = new Id<string>(1);
            var id3 = new Id<string>(2);
            var id4 = new Id<object>(2);

            Assert.Equal(id1, id2);
            Assert.NotEqual(id2, id3);
            Assert.True(id1 == id2);
            Assert.False(id1 != id2);
            Assert.False(id2 == id3);
            Assert.True(id2 != id3);
            Assert.NotEqual((object)id3, id4);
        }
    }

    public class TheExplicitConversionFromInt32
    {
        [Theory]
        [InlineData(0)]
        [InlineData(42)]
        public void ReturnsValue(int value)
        {
            var id = (Id<object>)value;

            Assert.Equal(value, id.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(42)]
        public void ReturnsNullableValue(int? value)
        {
            var id = (Id<object>?)value;

            Assert.Equal(value, id?.Value);
        }
    }

    public class TheImplicitConversionToInt32
    {
        [Fact]
        public void ReturnsValue()
        {
            var id = new Id<object>(42);

            int value = id;

            Assert.Equal(42, value);
        }
    }

    public class TheParseMethod
    {
        [Fact]
        public void CanParseIntStringToId()
        {
            Assert.Equal(42, Id<object>.Parse("42"));
        }

        [Theory]
        [InlineData(null, typeof(ArgumentNullException))]
        [InlineData("", typeof(FormatException))]
        [InlineData("O", typeof(FormatException))]
        public void ThrowsGivenInvalidId(string? s, Type expected)
        {
            Assert.Throws(expected, () => Id<object>.Parse(s));
        }
    }

    public class TheTryParseMethod
    {
        [Fact]
        public void CanParseIntStringToId()
        {
            Assert.True(Id<object>.TryParse("42", out var id));
            Assert.Equal(42, id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("O")]
        public void ReturnsFalseGivenInvalidId(string? s)
        {
            Assert.False(Id<object>.TryParse(s, out var id));
            Assert.Equal(0, id);
        }
    }

    public class TheToStringMethod
    {
        [Fact]
        public void ReturnsIdValueAsString()
        {
            var id = new Id<IdTests>(8675309);
            Assert.Equal("8675309", id.ToString());
        }
    }

    public class NewtonsoftJsonSerialization
    {
        [Fact]
        public void CanBeSerialized()
        {
            var source = new { Id = new Id<IdTests>(42), Name = "whatevs" };

            var json = JsonConvert.SerializeObject(source);

            Assert.Equal("""{"Id":42,"Name":"whatevs"}""", json);
        }

        [Fact]
        public void CanBeDeserialized()
        {
            const string json = """{"Id":23,"Name":"whatevs"}""";

            var result = JsonConvert.DeserializeObject<IdContainer>(json);

            Assert.NotNull(result);
            Assert.Equal(23, result.Id);
            Assert.Equal("whatevs", result.Name);
        }

        [Fact]
        public void CanDeserializeIntToId()
        {
            var id = new Id<string>(123);
            var json = JsonConvert.SerializeObject(id);
            Assert.Equal("123", json);
            var deserialized = JsonConvert.DeserializeObject<Id<string>>(json);
            Assert.Equal(id, deserialized);
        }

        [Fact]
        public void CanDeserializeNullableIntToNullableId()
        {
            var sample = new NullableIdContainer(new Id<NullableIdContainer>(123), "name");
            var json = JsonConvert.SerializeObject(sample);
            Assert.NotNull(json);

            var deserialized = JsonConvert.DeserializeObject<NullableIdContainer>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(new Id<NullableIdContainer>(123), deserialized.Id);
        }

        [Fact]
        public void CanDeserializeNullIntToNullableId()
        {
            var sample = new NullableIdContainer(null, "Name");
            var json = JsonConvert.SerializeObject(sample);
            Assert.NotNull(json);

            var deserialized = JsonConvert.DeserializeObject<NullableIdContainer>(json);

            Assert.NotNull(deserialized);
            Assert.Null(deserialized.Id);
        }

        public record IdContainer(Id<object> Id, string Name);

        public record NullableIdContainer(Id<NullableIdContainer>? Id, string Name);

    }

    public class SystemTextJsonSerialization
    {
        [Fact]
        public void CanBeSerialized()
        {
            var source = new { Id = new Id<IdTests>(42), Name = "whatevs" };

            var json = STJ.JsonSerializer.Serialize(source);

            Assert.Equal("""{"Id":42,"Name":"whatevs"}""", json);
        }

        [Fact]
        public void CanBeDeserialized()
        {
            const string json = """{"Id":23,"Name":"whatevs"}""";

            var result = STJ.JsonSerializer.Deserialize<IdContainer>(json);

            Assert.NotNull(result);
            Assert.Equal(23, result.Id);
            Assert.Equal("whatevs", result.Name);
        }

        [Fact]
        public void LegacyValuesCanBeDeserialized()
        {
            const string json = """{"Id":{"Value":23},"Name":"whatevs"}""";

            var result = STJ.JsonSerializer.Deserialize<IdContainer>(json);

            Assert.NotNull(result);
            Assert.Equal(23, result.Id);
            Assert.Equal("whatevs", result.Name);
        }

        [Fact]
        public void CanDeserializeIntToId()
        {
            var id = new Id<string>(123);
            var json = STJ.JsonSerializer.Serialize(id);
            Assert.Equal("123", json);
            var deserialized = STJ.JsonSerializer.Deserialize<Id<string>>(json);
            Assert.Equal(id, deserialized);
        }

        [Fact]
        public void CanDeserializeNullableIntToNullableId()
        {
            var sample = new NullableIdContainer(new Id<NullableIdContainer>(123), "name");
            var json = STJ.JsonSerializer.Serialize(sample);
            Assert.NotNull(json);

            var deserialized = STJ.JsonSerializer.Deserialize<NullableIdContainer>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(new Id<NullableIdContainer>(123), deserialized.Id);
        }

        [Fact]
        public void CanDeserializeNullIntToNullableId()
        {
            var sample = new NullableIdContainer(null, "Name");
            var json = STJ.JsonSerializer.Serialize(sample);
            Assert.NotNull(json);

            var deserialized = STJ.JsonSerializer.Deserialize<NullableIdContainer>(json);

            Assert.NotNull(deserialized);
            Assert.Null(deserialized.Id);
        }

        public record IdContainer(Id<object> Id, string Name);

        public record NullableIdContainer(Id<NullableIdContainer>? Id, string Name);

    }
}
