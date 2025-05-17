using FluentAssertions;
using Xunit.Abstractions;
using System.Globalization;

namespace DollarSignEngine.Tests;

public class ComprehensiveInterpolationTests : TestBase
{
    public ComprehensiveInterpolationTests(ITestOutputHelper output) : base(output)
    {
        DollarSign.ClearCache();
    }

    [Fact]
    public async Task Should_Match_CSharp_Simple_Variable_Interpolation()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", parameters);
        var expected = $"Hello, {parameters.name}!";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Multiple_Variables_Interpolation()
    {
        // Arrange
        var parameters = new { firstName = "John", lastName = "Doe", age = 30 };

        // Act
        var result = await DollarSign.EvalAsync("Name: {firstName} {lastName}, Age: {age}", parameters);
        var expected = $"Name: {parameters.firstName} {parameters.lastName}, Age: {parameters.age}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Empty_Expression_Handling()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("", parameters);
        var expected = $"";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Null_Variables_Handling()
    {
        // Arrange
        var parameters = new { name = (string)null };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}!", parameters);
        var expected = $"Hello, {parameters.name}!";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Escaped_Braces_Handling()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {{name}}, your code is: {{code}}", parameters);
        var expected = $"Hello, {{name}}, your code is: {{code}}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Mixed_Escaped_And_Interpolated_Braces()
    {
        // Arrange
        var parameters = new { name = "John" };

        // Act
        var result = await DollarSign.EvalAsync("Hello, {name}, your code is: {{code-{name}}}", parameters);
        var expected = $"Hello, {parameters.name}, your code is: {{code-{parameters.name}}}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Complex_Objects_Interpolation()
    {
        // Arrange
        var person = new
        {
            Name = "John",
            Address = new
            {
                Street = "123 Main St",
                City = "Anytown",
                ZipCode = "12345"
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Name: {Name}, Address: {Address.Street}, {Address.City} {Address.ZipCode}", person);
        var expected = $"Name: {person.Name}, Address: {person.Address.Street}, {person.Address.City} {person.Address.ZipCode}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Dictionary_Interpolation()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            ["name"] = "John",
            ["age"] = 30
        };

        // Act - 사전 값을 직접 전달
        var result = await DollarSign.EvalAsync("Name: {name}, Age: {age}", parameters);

        // C#에서는 사전 값을 직접 보간할 수 없으므로 수동으로 기대 결과 구성
        var expected = $"Name: {parameters["name"]}, Age: {parameters["age"]}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Format_Specifiers()
    {
        // Arrange
        var parameters = new
        {
            price = 123.456,
            date = new DateTime(2023, 10, 15)
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Price: {price:C2}, Date: {date:yyyy-MM-dd}", parameters);
        var expected = $"Price: {parameters.price:C2}, Date: {parameters.date:yyyy-MM-dd}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Special_Characters_Handling()
    {
        // Arrange
        var parameters = new
        {
            name = "John",
            symbols = "!@#$%^&*()_+<>?:\"{}|~`[]\\;',./",
            koreanText = "안녕하세요",
            emojiText = "😊🎉🚀"
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Name: {name}, Symbols: {symbols}, Korean: {koreanText}, Emoji: {emojiText}", parameters);
        var expected = $"Name: {parameters.name}, Symbols: {parameters.symbols}, Korean: {parameters.koreanText}, Emoji: {parameters.emojiText}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Newlines_And_Tabs()
    {
        // Arrange
        var parameters = new { name = "John\nDoe", address = "123 Main St\r\nAnytown, ST\t12345" };

        // Act
        var result = await DollarSign.EvalAsync(
            "Name:\n{name}\nAddress:\t{address}", parameters);
        var expected = $"Name:\n{parameters.name}\nAddress:\t{parameters.address}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Multiple_Format_Specifiers()
    {
        // Arrange
        var parameters = new
        {
            price = 1234.56,
            quantity = 42,
            percentage = 0.1234,
            date = new DateTime(2023, 12, 31)
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Price: {price:C2}, Qty: {quantity:D3}, Percent: {percentage:P1}, Date: {date:yyyy-MM-dd}",
            parameters);
        var expected = $"Price: {parameters.price:C2}, Qty: {parameters.quantity:D3}, Percent: {parameters.percentage:P1}, Date: {parameters.date:yyyy-MM-dd}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Various_Data_Types()
    {
        // Arrange
        var parameters = new
        {
            stringValue = "text",
            intValue = 42,
            doubleValue = 123.456,
            decimalValue = 789.012m,
            boolValue = true,
            dateValue = new DateTime(2023, 12, 31),
            guidValue = Guid.NewGuid(),
            nullValue = (string)null
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "String: {stringValue}, Int: {intValue}, Double: {doubleValue}, " +
            "Decimal: {decimalValue}, Bool: {boolValue}, Date: {dateValue}, " +
            "Guid: {guidValue}, Null: {nullValue}",
            parameters);
        var expected = $"String: {parameters.stringValue}, Int: {parameters.intValue}, Double: {parameters.doubleValue}, " +
                      $"Decimal: {parameters.decimalValue}, Bool: {parameters.boolValue}, Date: {parameters.dateValue}, " +
                      $"Guid: {parameters.guidValue}, Null: {parameters.nullValue}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Deeply_Nested_Properties()
    {
        // Arrange
        var complexObject = new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Level3 = new
                    {
                        Level4 = new
                        {
                            Value = "DeepValue"
                        }
                    }
                }
            }
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Deep value: {Level1.Level2.Level3.Level4.Value}", complexObject);
        var expected = $"Deep value: {complexObject.Level1.Level2.Level3.Level4.Value}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Alignment_Format_Specifiers()
    {
        // Arrange
        var parameters = new
        {
            left = "Left",
            right = "Right",
            center = "Center"
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Left aligned: {left,-10}|, Right aligned: {right,10}|, Default: {center}|",
            parameters);
        var expected = $"Left aligned: {parameters.left,-10}|, Right aligned: {parameters.right,10}|, Default: {parameters.center}|";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Culture_Specific_Formatting()
    {
        // Arrange
        var parameters = new
        {
            amount = 1234.56
        };

        // 현재 문화권 저장
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // 미국 문화권 설정
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // Act - 미국 문화권에서의 테스트
            var usResult = await DollarSign.EvalAsync("Amount: {amount:C}", parameters);
            var usExpected = $"Amount: {parameters.amount:C}";

            // 독일 문화권 설정
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            // Act - 독일 문화권에서의 테스트
            var deResult = await DollarSign.EvalAsync("Amount: {amount:C}", parameters);
            var deExpected = $"Amount: {parameters.amount:C}";

            // Assert
            usResult.Should().Be(usExpected);
            deResult.Should().Be(deExpected);

            // 미국과 독일 포맷이 다른지도 검증
            usResult.Should().NotBe(deResult);
        }
        finally
        {
            // 테스트 후 원래 문화권 복원
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Should_Match_CSharp_Missing_Properties_Behavior()
    {
        // Arrange
        var parameters = new { existingProp = "value" };

        // Act
        var result = await DollarSign.EvalAsync(
            "Existing: {existingProp}, Missing: {missingProp}", parameters);

        // C#에서 CompileTimeException이 발생하므로 수동으로 기대 결과 구성
        // DollarSignEngine에서는 빈 문자열로 처리되므로 이 동작을 확인
        var expected = "Existing: value, Missing: ";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Object_ToString_Behavior()
    {
        // Arrange
        var customObject = new CustomToStringObject("Custom Value");
        var parameters = new { obj = customObject };

        // Act
        var result = await DollarSign.EvalAsync("Object: {obj}", parameters);
        var expected = $"Object: {parameters.obj}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_Quotes_And_Escapes()
    {
        // Arrange
        var parameters = new
        {
            quotes = "He said \"Hello\" and 'Hi'",
            escapes = "\\path\\to\\file.txt"
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Quotes: {quotes}, Escapes: {escapes}", parameters);
        var expected = $"Quotes: {parameters.quotes}, Escapes: {parameters.escapes}";

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Should_Match_CSharp_EnumToString_Behavior()
    {
        // Arrange
        var parameters = new
        {
            day = DayOfWeek.Wednesday,
            access = FileAccess.ReadWrite
        };

        // Act
        var result = await DollarSign.EvalAsync(
            "Day: {day}, Access: {access}", parameters);
        var expected = $"Day: {parameters.day}, Access: {parameters.access}";

        // Assert
        result.Should().Be(expected);
    }

    // C# 보간 문자열 동작 복제 여부를 확인하기 위한 도우미 클래스
    public class CustomToStringObject
    {
        private readonly string _value;

        public CustomToStringObject(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return $"CustomObject[{_value}]";
        }
    }
}