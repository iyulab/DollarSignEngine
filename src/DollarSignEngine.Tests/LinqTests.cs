using DollarSignEngine;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DollarSignEngine.Tests
{
    // 테스트에 사용할 공개 클래스 정의
    public class TestData
    {
        public int[] Numbers { get; set; } = Array.Empty<int>();
        public string[] Names { get; set; } = Array.Empty<string>();
        public Person[] People { get; set; } = Array.Empty<Person>();
    }

    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class LinqTests : TestBase
    {
        public LinqTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task Should_Support_Where()
        {
            // Arrange
            var data = new TestData
            {
                Numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }
            };
            string template = "Even numbers: {string.Join(\", \", Numbers.Where(n => n % 2 == 0))}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);

            // Assert
            var expected = $"Even numbers: {string.Join(", ", data.Numbers.Where(n => n % 2 == 0))}";
            result.Should().Be(expected);
        }

        [Fact]
        public async Task Should_Support_Select()
        {
            // Arrange
            var data = new TestData
            {
                Numbers = new[] { 1, 2, 3, 4, 5 }
            };
            string template = "Squares: {string.Join(\", \", Numbers.Select(n => n * n))}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);

            // Assert
            var expected = $"Squares: {string.Join(", ", data.Numbers.Select(n => n * n))}";
            result.Should().Be(expected);
        }

        [Fact]
        public async Task Should_Support_OrderBy()
        {
            // Arrange
            var data = new TestData
            {
                Names = new[] { "David", "Alice", "Charlie", "Bob" }
            };
            string template = "Sorted names: {string.Join(\", \", Names.OrderBy(n => n))}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);

            // Assert
            var expected = $"Sorted names: {string.Join(", ", data.Names.OrderBy(n => n))}";
            result.Should().Be(expected);
        }

        [Fact]
        public async Task Should_Support_Complex_Objects()
        {
            // Arrange
            var data = new TestData
            {
                People = new[]
                {
                    new Person { Name = "Alice", Age = 30 },
                    new Person { Name = "Bob", Age = 25 },
                    new Person { Name = "Charlie", Age = 35 }
                }
            };
            string template = "Adults: {string.Join(\", \", People.Where(p => p.Age >= 30).Select(p => p.Name))}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);

            // Assert
            var expected = $"Adults: {string.Join(", ", data.People.Where(p => p.Age >= 30).Select(p => p.Name))}";
            result.Should().Be(expected);
        }

        [Fact]
        public async Task Should_Support_Aggregation()
        {
            // Arrange
            var data = new TestData
            {
                Numbers = new[] { 1, 2, 3, 4, 5 }
            };
            string template = "Sum: {Numbers.Sum()}, Average: {Numbers.Average():F1}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);
            _output.WriteLine($"Template: {template}");
            _output.WriteLine($"Result: {result}");

            // Assert - manually calculate expected values for clarity
            decimal sum = data.Numbers.Sum();
            string formattedAverage = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1}", data.Numbers.Average());
            var expected = $"Sum: {sum}, Average: {formattedAverage}";
            _output.WriteLine($"Expected: {expected}");
            result.Should().Be(expected);
        }

        [Fact]
        public async Task Should_Support_Multiple_Format_Specifiers()
        {
            // Arrange
            var data = new TestData
            {
                Numbers = new[] { 1234, 5678, 9012 }
            };
            string template = "Currency: {Numbers.Sum():C2}, Number: {Numbers.Average():N1}, Percent: {(Numbers.Average() / 10000):P2}";

            // Act
            var result = await DollarSign.EvalAsync(template, data);
            _output.WriteLine($"Template: {template}");
            _output.WriteLine($"Result: {result}");

            // Assert - manually calculate with explicit format
            decimal sum = data.Numbers.Sum();
            double avg = data.Numbers.Average();
            string formattedSum = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:C2}", sum);
            string formattedAvg = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:N1}", avg);
            string formattedPercent = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:P2}", avg / 10000);

            var expected = $"Currency: {formattedSum}, Number: {formattedAvg}, Percent: {formattedPercent}";
            _output.WriteLine($"Expected: {expected}");

            result.Should().Be(expected);
        }
    }
}