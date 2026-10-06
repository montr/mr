using System.Globalization;
using System.Text.Json;
using Monoreport.Models;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class FormatterTests
    {
        [Theory]
        [InlineData("125430.50", "Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек")]
        [InlineData("0", "Ноль рублей 00 копеек")]
        [InlineData("1.01", "Один рубль 01 копейка")]
        [InlineData("2.02", "Два рубля 02 копейки")]
        [InlineData("5.05", "Пять рублей 05 копеек")]
        [InlineData("11.11", "Одиннадцать рублей 11 копеек")]
        [InlineData("21.21", "Двадцать один рубль 21 копейка")]
        [InlineData("-22.22", "Минус двадцать два рубля 22 копейки")]
        [InlineData("1000", "Одна тысяча рублей 00 копеек")]
        [InlineData("2000", "Две тысячи рублей 00 копеек")]
        [InlineData("1000001", "Один миллион один рубль 00 копеек")]
        [InlineData("1.995", "Два рубля 00 копеек")]
        [InlineData("-0.01", "Минус ноль рублей 01 копейка")]
        public void Spells_rubles(string amount, string expected)
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Amount"))), "{\"Amount\":" + amount + "}", new() { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Amount"] = new("spellOut") } });
            Assert.Equal(expected, Docx.Text(Docx.Read(result.Document)));
            Assert.DoesNotContain("spellOut", Docx.Read(result.Document).ToString());
        }

        [Fact]
        public void Supports_custom_formatter_registration()
        {
            var registry = new FormatterRegistry(new Dictionary<string, IValueFormatter>());
            registry.Register("prefix", new PrefixFormatter());
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))), "{\"Name\":\"test\"}", new() { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Name"] = new("prefix", new Dictionary<string, string> { ["prefix"] = "custom:" }) } }, registry);
            Assert.Equal("custom:test", Docx.Text(Docx.Read(result.Document)));
        }

        [Fact]
        public void Unknown_custom_formatter_reports_context()
        {
            var error = Assert.Throws<TemplateException>(() => Docx.Merge(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Name"))), "{\"Name\":1}", new() { FieldFormatters = new Dictionary<string, FormatterBinding> { ["Name"] = new("unknown") } }));
            Assert.Equal("unknown", error.Formatter);
            Assert.Equal("Name", error.Field);
            Assert.Equal("MERGEFIELD Name", error.FieldCode);
        }

        [Fact]
        public void Uses_explicit_culture_for_number_formatting()
        {
            var result = Docx.Merge(Docx.Create(Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\""))), "{\"Amount\":12.5}", new() { Culture = CultureInfo.GetCultureInfo("ru-RU") });
            Assert.Equal("12,50", Docx.Text(Docx.Read(result.Document)));
        }

        private sealed class PrefixFormatter : IValueFormatter
        {
            public string Format(JsonElement value, CultureInfo culture, IReadOnlyDictionary<string, string> options) => options["prefix"] + value.GetString();
        }
    }
}
