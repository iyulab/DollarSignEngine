using System.Dynamic;

namespace DollarSignEngine.Tests
{
    public class DollarSignEngineTests
    {
        [Fact]
        public async Task ShouldReturnStringWithCurrentDate_WhenExpressionIncludesDateTimeNow()
        {
            var expression = "today is {DateTime.Now:yyyy-MM-dd}";
            var result = await DollarSign.EvalAsync(expression);

            Assert.Equal($"today is {DateTime.Now:yyyy-MM-dd}", result);
        }

        [Fact]
        public async Task ShouldInterpolateStringWithGivenParameters_WhenExpressionIncludesParameterPlaceholders()
        {
            var name = "John";
            var parameters = new Dictionary<string, object?>()
            {
                { "name", name }
            };

            var expression = "my name is {name}";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"my name is {name}", result);
        }

        [Fact]
        public async Task ShouldInterpolateStringWithMultipleParameters_WhenExpressionIncludesMultiplePlaceholders()
        {
            var parameters = new Dictionary<string, object?>()
            {
                { "firstName", "John" },
                { "lastName", "Doe" }
            };

            var expression = "Hello, my name is {firstName} {lastName}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Hello, my name is {parameters["firstName"]} {parameters["lastName"]}.", result);
        }

        [Fact]
        public async Task ShouldHandleComplexDataTypes_WhenParameterIsComplexType()
        {
            var person = new { FirstName = "Jane", LastName = "Doe" };
            var parameters = new Dictionary<string, object?>()
            {
                { "person", person }
            };

            var expression = "The person's full name is {person.FirstName} {person.LastName}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"The person's full name is {person.FirstName} {person.LastName}.", result);
        }

        [Fact]
        public async Task ShouldEvaluateConditionalStatements_WhenExpressionIncludesIfElse()
        {
            var age = 20;
            var parameters = new Dictionary<string, object?>()
            {
                { "age", age }
            };

            var expression = "You are {(age >= 18 ? \"adult\" : \"minor\")}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"You are {(age >= 18 ? "adult" : "minor")}.", result);
        }

        [Fact]
        public async Task ShouldNotEvaluateLoops_WhenExpressionIncludesLoops()
        {
            // 이 테스트는 실패할 것입니다. C# 스크립트 평가가 반복문을 처리할 수 있는지 확인하기 위함입니다.
            var parameters = new Dictionary<string, object?>();
            var expression = "for(int i = 0; i < 5; i++) {}";
            await Assert.ThrowsAsync<DollarSignEngineException>(() => DollarSign.EvalAsync(expression, parameters));
        }

        [Fact]
        public async Task ShouldAccessCollectionItem_WhenParameterIsCollection()
        {
            var numbers = new List<int> { 1, 2, 3, 4, 5 };
            var parameters = new Dictionary<string, object?>()
            {
                { "numbers", numbers }
            };

            var expression = "The second number is {numbers[1]}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"The second number is {numbers[1]}.", result);
        }

        [Fact]
        public async Task ShouldHandleDateTimeFormat_WhenExpressionIncludesDateTimeFormatting()
        {
            var parameters = new Dictionary<string, object?>()
            {
                { "today", DateTime.Now }
            };

            var expression = "The year is {today:yyyy}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"The year is {DateTime.Now:yyyy}.", result);
        }

        [Fact]
        public async Task ShouldReturnStringBasedOnBooleanValue_WhenExpressionIncludesBooleanParameter()
        {
            var isMember = true;
            var parameters = new Dictionary<string, object?>()
            {
                { "isMember", isMember }
            };

            var expression = "Membership status: {(isMember ? \"Active\" : \"Inactive\")}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Membership status: {(isMember ? "Active" : "Inactive")}.", result);
        }

        [Fact]
        public async Task ShouldCalculateSumOfListElements_WhenExpressionOperatesOnListParameter()
        {
            var numbers = new List<int> { 1, 2, 3 };
            var parameters = new Dictionary<string, object?>()
            {
                { "numbers", numbers }
            };

            var expression = "Total sum: {numbers.Sum()}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Total sum: {numbers.Sum()}.", result);
        }

        [Fact]
        public async Task ShouldAccessPropertyOfAnonymousObject_WhenExpressionIncludesAnonymousObjectParameter()
        {
            var product = new { Name = "Book", Price = 9.99 };
            var parameters = new Dictionary<string, object?>()
            {
                { "product", product }
            };

            var expression = "Product: {product.Name}, Price: {product.Price}";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Product: {product.Name}, Price: {product.Price}", result);
        }

        [Fact]
        public async Task ShouldEvaluateExpressionWithMultipleConditions_WhenExpressionIncludesIfElseIfElse()
        {
            var score = 85;
            var parameters = new Dictionary<string, object?>()
            {
                { "score", score }
            };

            var expression = "Grade: {(score >= 90 ? \"A\" : score >= 80 ? \"B\" : \"C\")}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Grade: {(score >= 90 ? "A" : score >= 80 ? "B" : "C")}.", result);
        }

        [Fact]
        public async Task ShouldAccessPropertyOfComplexTypeArrayElement_WhenParameterIsArrayOfComplexType()
        {
            var peoples = new[] { new { Name = "Jane", Age = 30 }, new { Name = "John", Age = 25 } };
            var parameters = new Dictionary<string, object?>()
            {
                { "peoples", peoples }
            };

            var expression = "Second person: {peoples[1].Name}, Age: {peoples[1].Age}.";
            var result = await DollarSign.EvalAsync(expression, parameters);

            Assert.Equal($"Second person: {peoples[1].Name}, Age: {peoples[1].Age}.", result);
        }

        public class User
        {
            public string Username { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        [Fact]
        public async Task ShouldInterpolateCustomObjectProperties_WhenExpressionIncludesCustomObject()
        {
            var user = new User { Username = "Alice", Age = 30 };
            var parameters = new Dictionary<string, object?> { { "user", user } };
            var expression = "User: {user.Username}, Age: {user.Age}";
            var result = await DollarSign.EvalAsync(expression, parameters);
            Assert.Equal($"User: {user.Username}, Age: {user.Age}", result);
        }

        [Fact]
        public async Task ShouldInterpolateDictionaryValues_WhenExpressionIncludesDictionaryKey()
        {
            var settings = new Dictionary<string, string> { { "Theme", "Dark" }, { "FontSize", "12" } };
            var parameters = new Dictionary<string, object?> { { "settings", settings } };
            var expression = "Theme: {settings[\"Theme\"]}, Font Size: {settings[\"FontSize\"]}";
            var result = await DollarSign.EvalAsync(expression, parameters);
            Assert.Equal($"Theme: {settings["Theme"]}, Font Size: {settings["FontSize"]}", result);
        }

        [Fact]
        public async Task ShouldAccessMultiDimensionalArrayElement_WhenParameterIsMultiDimensionalArray()
        {
            var matrix = new int[,] { { 1, 2 }, { 3, 4 } };
            var parameters = new Dictionary<string, object?> { { "matrix", matrix } };
            var expression = "Element: {matrix[1, 1]}";
            var result = await DollarSign.EvalAsync(expression, parameters);
            Assert.Equal($"Element: {matrix[1, 1]}", result);
        }

        [Fact]
        public async Task ShouldUseLinqQueryResult_WhenExpressionOperatesOnCollections()
        {
            var numbers = new List<int> { 1, 2, 3, 4, 5 };
            var parameters = new Dictionary<string, object?> { { "numbers", numbers } };
            var expression = "Count greater than 2: {numbers.Where(n => n > 2).Count()}";
            var result = await DollarSign.EvalAsync(expression, parameters);
            Assert.Equal($"Count greater than 2: {numbers.Where(n => n > 2).Count()}", result);
        }

        [Fact]
        public async Task ShouldHandleDynamicObjectProperties_WhenExpressionIncludesDynamicObject()
        {
            dynamic person = new ExpandoObject();
            person.Name = "Bob";
            person.Age = 25;
            var parameters = new Dictionary<string, object?> { { "person", person } };
            var expression = "Person: {person.Name}, Age: {person.Age}";
            var result = await DollarSign.EvalAsync(expression, parameters);
            Assert.Equal($"Person: {person.Name}, Age: {person.Age}", result);
        }

        [Fact]
        public async Task ShouldInterpolatePropertiesOfCustomObjectParameter_WhenExpressionIncludesCustomObject()
        {
            var person = new User() { Username = "Alice", Age = 30 };
            var expression = "Person: {person.Username}, Age: {person.Age}";
            var result = await DollarSign.EvalAsync(expression, new { person });

            Assert.Equal($"Person: {person.Username}, Age: {person.Age}", result);
        }
    }
}