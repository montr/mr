using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MonoReport.Cli.Services;
using MonoReport.Services;

namespace MonoReport.Tests.Services
{
    public sealed class CliTests
    {
        private static CliApplication CreateApplication()
        {
            using var provider = new ServiceCollection().AddMonoReport().BuildServiceProvider();
            return new CliApplication(new CliOptionsParser(), provider.GetRequiredService<MergeEngine>(),
                provider.GetRequiredService<TemplateAnalyzer>());
        }
        private const string TemplateFileName = "sample.template.docx";
        private const string DataFileName = "sample.json";
        private const string ExpectedAmountInWords = "Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек";

        [Theory]
        [InlineData(true, "stdout")]
        [InlineData(false, "stdout")]
        [InlineData(true, "new")]
        [InlineData(false, "new")]
        [InlineData(true, "existing")]
        [InlineData(false, "existing")]
        public void Invalid_template_rejection_preserves_streams_and_output_files(bool nested, string mode)
        {
            var template = nested ? Docx.Create(Docx.P(Docx.NestedResult(false, false, "MERGEFIELD Name", "PAGE"))) : Docx.Create(Docx.ConflictingLayout("paragraph"));
            Docx.AssertValid(template);
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            var templatePath = Path.Combine(directory, "invalid.docx");
            var outputPath = Path.Combine(directory, "output.docx");
            var sentinel = Encoding.UTF8.GetBytes("existing output sentinel");
            File.WriteAllBytes(templatePath, template);
            if (mode == "existing") File.WriteAllBytes(outputPath, sentinel);
            try
            {
                var args = new List<string> { CliArguments.Template, templatePath, CliArguments.Json, "{\"Name\":\"replacement\",\"A\":[{}],\"B\":[{}]}" };
                if (mode != "stdout") args.AddRange(new[] { CliArguments.Output, outputPath });
                var app = CreateApplication();
                using var output = new MemoryStream();
                using var errors = new StringWriter();
                Assert.Equal(1, app.Run(args.ToArray(), Stream.Null, output, errors));
                Assert.Empty(output.ToArray());
                Assert.True(output.CanWrite);
                Assert.Contains("/word/document.xml", errors.ToString());
                Assert.Contains("paragraph 1", errors.ToString());
                Assert.Contains("unsupported", errors.ToString());
                Assert.Contains(nested ? "plain cached text" : "separate supported", errors.ToString());
                Assert.DoesNotContain("Stack Trace", errors.ToString());
                Assert.Equal(template, File.ReadAllBytes(templatePath));
                if (mode == "existing") Assert.Equal(sentinel, File.ReadAllBytes(outputPath));
                else Assert.False(File.Exists(outputPath));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void Non_bmp_value_generates_valid_stdout_with_preserved_fields_and_formatting()
        {
            var w = WordXml.RunElementName.Namespace;
            var field = new System.Xml.Linq.XElement(WordXml.SimpleFieldElementName, new System.Xml.Linq.XAttribute(WordXml.InstructionAttributeName, "MERGEFIELD Name \\* MERGEFORMAT"),
                new System.Xml.Linq.XElement(w + "r", Docx.Properties(), new System.Xml.Linq.XElement(w + "t", "x")),
                new System.Xml.Linq.XElement(w + "r", Docx.Properties(), new System.Xml.Linq.XElement(w + "t", "y")));
            var template = Docx.Create(Docx.P(field));
            Docx.AssertValid(template);
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, template);
            try
            {
                var app = CreateApplication();
                using var output = new MemoryStream();
                using var errors = new StringWriter();
                Assert.Equal(0, app.Run([CliArguments.Template, path, CliArguments.Json, "{\"Name\":\"😀 crane 🚧\"}"], Stream.Null, output, errors));
                Assert.Empty(errors.ToString());
                Docx.AssertValid(output.ToArray());
                var xml = Docx.Read(output.ToArray());
                Assert.Equal("😀 crane 🚧", Docx.Text(xml));
                Assert.Equal("MERGEFIELD Name \\* MERGEFORMAT", (string?)Assert.Single(xml.Descendants(WordXml.SimpleFieldElementName)).Attribute(WordXml.InstructionAttributeName));
                Assert.Equal(2, xml.Descendants(WordXml.RunPropertiesElementName).Count());
                Assert.All(xml.Descendants(WordXml.RunPropertiesElementName), p => Assert.True(System.Xml.Linq.XNode.DeepEquals(Docx.Properties(), p)));
                Assert.Equal(template, File.ReadAllBytes(path));
            }
            finally { File.Delete(path); }
        }

        [Theory]
        [InlineData("--json")]
        [InlineData("--json-file")]
        [InlineData("--stdin")]
        [InlineData("")]
        public void Renders_sample_from_each_json_source_to_binary_stdout(string source)
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, TemplateFileName);
            var dataPath = Path.Combine(AppContext.BaseDirectory, DataFileName);
            var json = File.ReadAllText(dataPath);
            var args = new List<string> { CliArguments.Template, templatePath };
            if (source.Length > 0)
            {
                args.Add(source);
            }

            if (source == CliArguments.Json)
            {
                args.Add(json);
            }
            else if (source == CliArguments.JsonFile)
            {
                args.Add(dataPath);
            }

            var application = CreateApplication();
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                using (var output = new MemoryStream())
                {
                    using (var errors = new StringWriter())
                    {
                        Assert.Equal(0, application.Run(args.ToArray(), input, output, errors));
                        Assert.Equal(string.Empty, errors.ToString());
                        Assert.True(input.CanRead);
                        Assert.True(output.CanWrite);
                        var bytes = output.ToArray();
                        Docx.AssertValid(File.ReadAllBytes(templatePath));
                        Docx.AssertValid(bytes);
                        var xml = Docx.Read(bytes);
                        var text = Docx.Text(xml);
                        Assert.Contains("Northwind Construction LLC", text);
                        Assert.Contains("7701234567", text);
                        Assert.Contains("MAT-001/2026", text);
                        Assert.Contains("15 Jan 2026", text);
                        Assert.Contains("Washed building sand", text);
                        Assert.Contains("Material items: 3", text);
                        Assert.Contains("125,430.50", text);
                        Assert.Contains(ExpectedAmountInWords, text);
                        Assert.Equal(4, xml.Descendants(WordXml.TableRowElementName).Count());
                        Assert.DoesNotContain("TableStart:", xml.ToString());
                        AssertSample(bytes);
                    }
                }
            }
        }


        [Fact]
        public void Committed_sample_is_valid_and_preserves_fields_and_formatting()
        {
            AssertSample(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sample.docx")));
        }

        private static void AssertSample(byte[] bytes)
        {
            Docx.AssertValid(bytes);
            var xml = Docx.Read(bytes);
            var text = Docx.Text(xml);
            Assert.Contains("Construction Project Cost Estimate", text);
            Assert.Contains("Riverside Community Centre", text);
            Assert.Contains("Northwind Construction LLC", text);
            Assert.Contains("1167746123456", text);
            Assert.Contains("James Carter", text);
            Assert.Contains("Portland cement (25 kg bag)", text);
            Assert.Contains("Concrete paving slabs (600 x 600 x 50 mm)", text);
            Assert.Contains("Washed building sand", text);
            Assert.Contains("1,308.61", text);
            Assert.Contains("65,430.50", text);
            Assert.Contains("125,430.50", text);
            Assert.Contains(ExpectedAmountInWords, text);
            var w = WordXml.TableRowElementName.Namespace;
            var rows = xml.Descendants(w + "tr").ToArray();
            Assert.Equal(4, rows.Length);
            Assert.All(rows, row => Assert.Equal(8, row.Elements(w + "tc").Count()));
            Assert.Equal(new[] { "Reference", "Delivery date", "Material", "Quantity", "Unit", "Unit price", "Line total", "Currency" },
                rows[0].Elements(w + "tc").Select(Docx.Text));
            Assert.Equal(new[] { "MAT-001/2026", "15 Jan 2026", "Portland cement (25 kg bag)", "100", "bags", "500.00", "50,000.00", "RUB" },
                rows[1].Elements(w + "tc").Select(Docx.Text));
            Assert.Single(xml.Descendants(w + "tblHeader"));
            Assert.Equal(8, rows[0].Descendants(w + "shd").Count());
            Assert.All(rows[0].Descendants(w + "shd"), shade => Assert.Equal("D9E5F2", (string?)shade.Attribute(w + "fill")));
            Assert.NotEmpty(xml.Descendants(w + "b"));
            Assert.Contains(xml.Descendants(w + "sz"), size => (string?)size.Attribute(w + "val") == "32");
            Assert.NotEmpty(xml.Descendants(w + "tblBorders"));
            Assert.Single(xml.Descendants(w + "sectPr"));
            var fields = xml.Descendants(WordXml.SimpleFieldElementName)
                .Select(field => (string?)field.Attribute(w + "instr") ?? string.Empty).ToArray();
            Assert.Equal(35, fields.Length);
            Assert.Contains(fields, field => field.Contains("Contractor.TaxId"));
            Assert.Contains(fields, field => field.Contains("Project.Name"));
            Assert.Equal(3, fields.Count(field => field.Contains("DeliveryDate") && field.Contains("dd MMM yyyy")));
            Assert.Equal(3, fields.Count(field => field.Contains("UnitPrice") && field.Contains("\\#")));
            Assert.Contains(fields, field => field.Contains("Totals.Amount") && field.Contains("SpellOut"));
            Assert.DoesNotContain("TableStart:", xml.ToString());
            Assert.DoesNotContain("TableEnd:", xml.ToString());
            Assert.DoesNotContain("«", text);
        }

        [Fact]
        public void Saves_file_and_does_not_write_docx_to_stdout()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".docx");
            var app = CreateApplication();

            try
            {
                using (var output = new MemoryStream())
                {
                    using (var errors = new StringWriter())
                    {
                        var code = app.Run([CliArguments.Template, Path.Combine(AppContext.BaseDirectory, TemplateFileName),
                            CliArguments.JsonFile, Path.Combine(AppContext.BaseDirectory, DataFileName),
                            CliArguments.Output, path], Stream.Null, output, errors);
                        Assert.Equal(0, code);
                        Assert.Equal(string.Empty, errors.ToString());
                        Assert.Empty(output.ToArray());
                        Docx.AssertValid(File.ReadAllBytes(path));
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData("{}", "--unknown", 2)]
        [InlineData("not-json", "--json", 1)]
        public void Errors_use_stderr_and_leave_stdout_empty(string json, string option, int expected)
        {
            var app = CreateApplication();
            using (var output = new MemoryStream())
            {
                using (var errors = new StringWriter())
                {
                    var result = app.Run([CliArguments.Template, Path.Combine(AppContext.BaseDirectory, TemplateFileName), option, json],
                        Stream.Null, output, errors);
                    Assert.Equal(expected, result);
                    Assert.Empty(output.ToArray());
                    Assert.NotEmpty(errors.ToString());
                }
            }
        }

        [Fact]
        public void Aggregated_generation_errors_preserve_existing_output_and_use_ordered_lines()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            var templatePath = Path.Combine(directory, "template.docx");
            var outputPath = Path.Combine(directory, "output.docx");
            var sentinel = Encoding.UTF8.GetBytes("existing output sentinel");
            File.WriteAllBytes(templatePath, Docx.Create(
                Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\"")),
                Docx.P(Docx.Field("MERGEFIELD Date \\@ \"yyyy-MM-dd\""))));
            File.WriteAllBytes(outputPath, sentinel);
            try
            {
                using var output = new MemoryStream();
                using var errors = new StringWriter();
                var exit = CreateApplication().Run([CliArguments.Template, templatePath, CliArguments.Json,
                    "{\"Amount\":\"bad\",\"Date\":\"bad\"}", CliArguments.Output, outputPath],
                    Stream.Null, output, errors);

                Assert.Equal(1, exit);
                Assert.Empty(output.ToArray());
                Assert.Equal(sentinel, File.ReadAllBytes(outputPath));
                var lines = errors.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(2, lines.Length);
                Assert.Contains("field=Amount", lines[0]);
                Assert.Contains("dataPath=Amount", lines[0]);
                Assert.Contains("field=Date", lines[1]);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void Generation_error_limit_prints_100_errors_and_one_informational_line()
        {
            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, Docx.Create(Enumerable.Range(0, 101)
                .Select(_ => Docx.P(Docx.Field("MERGEFIELD Amount \\# \"0.00\""))).ToArray()));
            try
            {
                using var output = new MemoryStream();
                using var errors = new StringWriter();
                var exit = CreateApplication().Run([CliArguments.Template, path, CliArguments.Json,
                    "{\"Amount\":\"bad\"}"], Stream.Null, output, errors);

                Assert.Equal(1, exit);
                Assert.Empty(output.ToArray());
                var lines = errors.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(101, lines.Length);
                Assert.All(lines[..100], line => Assert.StartsWith("generation.invalid_format:", line));
                Assert.Equal("Maximum of 100 errors reached; further errors may exist.", lines[100]);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Invalid_json_does_not_truncate_existing_output()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "existing content");

            try
            {
                var app = CreateApplication();
                using (var errors = new StringWriter())
                {
                    Assert.Equal(1, app.Run([CliArguments.Template, Path.Combine(AppContext.BaseDirectory, TemplateFileName),
                        CliArguments.Json, "invalid", CliArguments.Output, path], Stream.Null, Stream.Null, errors));
                    Assert.Equal("existing content", File.ReadAllText(path));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Theory]
        [InlineData("--template")]
        [InlineData("--output")]
        [InlineData("--json")]
        [InlineData("--json-file")]
        [InlineData("--culture")]
        public void Rejects_missing_option_values(string option)
        {
            Assert.Throws<ArgumentException>(() => new CliOptionsParser().Parse([option]));
        }

        [Fact]
        public void Rejects_conflicting_sources_and_duplicate_options()
        {
            var parser = new CliOptionsParser();
            Assert.Throws<ArgumentException>(() => parser.Parse([CliArguments.Template, TemplateFileName, CliArguments.Json, "{}", CliArguments.StandardInput]));
            Assert.Throws<ArgumentException>(() => parser.Parse([CliArguments.Template, TemplateFileName, CliArguments.Json, "{}", CliArguments.JsonFile, DataFileName]));
            Assert.Throws<ArgumentException>(() => parser.Parse([CliArguments.Template, TemplateFileName, CliArguments.TemplateShort, TemplateFileName]));
        }

        [Fact]
        public void Help_does_not_require_template_or_input()
        {
            var app = CreateApplication();
            using (var output = new MemoryStream())
            {
                using (var errors = new StringWriter())
                {
                    Assert.Equal(0, app.Run([CliArguments.Help], Stream.Null, output, errors));
                    Assert.Contains(CliArguments.Template, Encoding.UTF8.GetString(output.ToArray()));
                    Assert.Equal(string.Empty, errors.ToString());
                }
            }
        }
    }
}
