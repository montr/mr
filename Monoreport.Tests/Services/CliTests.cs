using System.Text;
using Monoreport.Cli.Services;
using Monoreport.Services;

namespace Monoreport.Tests.Services
{
    public sealed class CliTests
    {
        private const string TemplateFileName = "sample.template.docx";
        private const string DataFileName = "sample.json";
        private const string ExpectedAmountInWords = "Сто двадцать пять тысяч четыреста тридцать рублей 50 копеек";

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

            var application = new CliApplication(new CliOptionsParser(), Docx.CreateEngine());
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
                        Assert.Contains("ООО Ромашка", text);
                        Assert.Contains("7701234567", text);
                        Assert.Contains("Д-001/2026", text);
                        Assert.Contains("15.01.2026", text);
                        Assert.Contains("Консультационные услуги", text);
                        Assert.Contains("Всего договоров: 3", text);
                        Assert.Contains("125,430.50", text);
                        Assert.Contains(ExpectedAmountInWords, text);
                        Assert.Equal(4, xml.Descendants(WordXml.TableRowElementName).Count());
                        Assert.DoesNotContain("TableStart:", xml.ToString());
                        Assert.NotEmpty(xml.Descendants(WordXml.SimpleFieldElementName));
                    }
                }
            }
        }

        [Fact]
        public void Saves_file_and_does_not_write_docx_to_stdout()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".docx");
            var app = new CliApplication(new CliOptionsParser(), Docx.CreateEngine());

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
            var app = new CliApplication(new CliOptionsParser(), Docx.CreateEngine());
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
        public void Invalid_json_does_not_truncate_existing_output()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "existing content");

            try
            {
                var app = new CliApplication(new CliOptionsParser(), Docx.CreateEngine());
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
            var app = new CliApplication(new CliOptionsParser(), Docx.CreateEngine());
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
