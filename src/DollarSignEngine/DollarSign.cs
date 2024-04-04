using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Collections;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace DollarSignEngine
{
    /// <summary>
    /// Provides functionality to dynamically evaluate C# expressions using interpolation strings and parameters.
    /// It compiles and executes expressions at runtime, allowing the injection of variables and the construction of complex expressions.
    /// This class is static, implying its methods can be accessed without creating an instance of the class.
    /// </summary>
    public static class DollarSign
    {
        private static int expandoCounter = 0;

        /// <summary>
        /// Asynchronously evaluates a given C# expression as a string and returns the result.
        /// The method accepts an expression, optionally with parameters, compiles it into a runnable script, and evaluates it.
        /// This allows for the dynamic evaluation of expressions with embedded variables and complex logic.
        /// If the compilation or execution fails, it throws a DollarSignEngineException with details of the error.
        /// </summary>
        /// <param name="expression">The C# expression to evaluate. It can include interpolation strings and embedded expressions.</param>
        /// <param name="parameters">An optional dictionary of parameters to be injected into the expression. Each key-value pair represents a variable name and its value.</param>
        /// <returns>A Task that represents the asynchronous operation, resulting in the expression's evaluated string value.</returns>
        /// <exception cref="DollarSignEngineException">Thrown when the expression cannot be compiled or an exception occurs during evaluation.</exception>
        public static async Task<string> EvalAsync(string expression, IDictionary<string, object>? parameters = null)
        {
            try
            {
                expandoCounter = 0;

                var options = ScriptOptions.Default
                    .WithImports("System", "System.Collections.Generic", "System.Linq", "System.Dynamic")
                    .AddReferences(
                        Assembly.Load("mscorlib"),
                        Assembly.Load("System"),
                        Assembly.Load("System.Core"),
                        Assembly.Load("Microsoft.CSharp")
                    );

                var script = BuildCsScript(expression, parameters);
#if DEBUG
                Debug.WriteLine("[Logs]");
                Debug.WriteLine($"expression: {expression}");
                Debug.WriteLine($"script: {script}");
#endif
                var result = await CSharpScript.EvaluateAsync<string>(script, options);
                return result;
            }
            catch (CompilationErrorException compilationErrorException)
            {
                throw new DollarSignEngineException($"CompilationError: {compilationErrorException.Message}");
            }
            catch (Exception e)
            {
                throw new DollarSignEngineException($"Error: {e.Message}");
            }
        }

        public static async Task<string> EvalAsync(string expression, object parameter)
        {
            if (parameter is IDictionary)
            {
                return await EvalAsync(expression, parameter as IDictionary<string, object>);
            }

            var dic = new Dictionary<string, object?>();
            var properties = parameter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                dic.Add(prop.Name, prop.GetValue(parameter));
            }

            return await EvalAsync(expression, dic as IDictionary<string, object>);
        }

        private static readonly Dictionary<string, string> typeAliases = new()
        {
            { typeof(int).FullName!, "int" },
            { typeof(long).FullName!, "long" },
            { typeof(float).FullName!, "float" },
            { typeof(double).FullName!, "double" },
            { typeof(decimal).FullName!, "decimal" },
            { typeof(bool).FullName!, "bool" },
            { typeof(string).FullName!, "string" },
            { typeof(object).FullName!, "object" }
        };

        private static readonly Dictionary<Type, Func<object, string>> typeConverters = new()
        {
            { typeof(string), value => $"\"{value.ToString()!.Replace("\"", "\\\"")}\"" },
            { typeof(DateTime), value => $"DateTime.Parse(\"{value}\")" },
            { typeof(bool), value => value.ToString()!.ToLower() }, // 부울 값에 대한 처리 추가
        };

        private static string GetTypeAlias(Type type)
        {
            return typeAliases.TryGetValue(type.FullName!, out var alias) ? alias : type.Name;
        }

        private static void ConvertArrayElements(Array array, StringBuilder code, int[] lengths, int dimension, int[] indices, IDictionary<string, string> scriptParams)
        {
            if (dimension == lengths.Length)
            {
                var value = array.GetValue(indices);
                code.Append(ConvertValue(value!, scriptParams));
                return;
            }

            code.Append("{ ");
            for (int i = 0; i < lengths[dimension]; i++)
            {
                var newIndices = new List<int>(indices) { i }.ToArray();
                ConvertArrayElements(array, code, lengths, dimension + 1, newIndices, scriptParams);
                if (i < lengths[dimension] - 1) code.Append(", ");
            }
            code.Append(" }");
            //if (dimension == 0) code.Insert(0, "new ");
        }

        private static string ConvertValue(object value, IDictionary<string, string> scriptParams)
        {
            if (value == null) return "null";

            var type = value.GetType();
            if (typeConverters.TryGetValue(type, out var converter))
            {
                return converter(value);
            }
            else if (value is ExpandoObject expando)
            {
                var functionName = $"CreateExpandoObject{expandoCounter++}";
                var keyValuePairsCode = new StringBuilder();
                foreach (var kvp in (IDictionary<string, object>)expando!)
                {
                    var keyStr = kvp.Key;
                    var valueStr = ConvertValue(kvp.Value, scriptParams);
                    keyValuePairsCode.AppendLine($"expando.{keyStr} = {valueStr};");
                }

                scriptParams.Add(functionName, $@"
dynamic {functionName}()
{{
    dynamic expando = new System.Dynamic.ExpandoObject();
{keyValuePairsCode}
    return expando;
}}
");
                return $"{functionName}()";
            }
            else if (value is IDictionary dictionary && value.GetType().IsGenericType)
            {
                var dict = dictionary;
                var keyValuePairsCode = new StringBuilder();
                var keyType = value.GetType().GetGenericArguments()[0];
                var valueType = value.GetType().GetGenericArguments()[1];
                foreach (DictionaryEntry kvp in dict)
                {
                    var keyStr = ConvertValue(kvp.Key, scriptParams);
                    var valueStr = ConvertValue(kvp.Value!, scriptParams);
                    keyValuePairsCode.Append($"{{ {keyStr}, {valueStr} }}, ");
                }
                if (keyValuePairsCode.Length > 2) keyValuePairsCode.Length -= 2; // 마지막 쉼표 제거
                return $"new Dictionary<{keyType.Name}, {valueType.Name}>() {{ {keyValuePairsCode} }}";
            }
            else if (value is Array array && type.IsArray && type.GetElementType()!.Name.StartsWith("<>f__AnonymousType") != true)
            {
                var elementType = GetTypeAlias(type.GetElementType()!);
                var ranks = Enumerable.Range(0, type.GetArrayRank()).Select(array.GetLength).ToArray();
                var elements = new StringBuilder();
                ConvertArrayElements(array, elements, ranks, 0, [], scriptParams);

                return $"new {elementType}[{string.Join(',', ranks.Select(r => ""))}] {elements}";
            }
            else if (value is IEnumerable enumerable && value is not string)
            {
                var itemsCode = new StringBuilder();
                foreach (var item in enumerable)
                {
                    var itemCode = ConvertValue(item, scriptParams);
                    itemsCode.Append($"{itemCode}, ");
                }
                if (itemsCode.Length > 2) itemsCode.Length -= 2; // 마지막 쉼표 제거
                return $"new[] {{ {itemsCode} }}";
            }
            else if (type.FullName!.StartsWith("<>f__AnonymousType") || !type.IsPrimitive && !type.IsEnum && type != typeof(string))
            {
                // 익명 객체 및 사용자 정의 타입 처리
                var props = type.GetProperties();
                var objInit = string.Join(", ", props.Select(p => $"{p.Name} = {ConvertValue(p.GetValue(value)!, scriptParams)}"));
                return $"new {{ {objInit} }}";
            }
            else if (type == typeof(string))
            {
                return $"\"{value.ToString()!.Replace("\"", "\\\"")}\"";
            }
            else
            {
                return value.ToString()!;
            }
        }

        public static string BuildCsScript(string expression, IDictionary<string, object>? parameters)
        {
            var declarations = new StringBuilder();
            var getFunctions = new StringBuilder();
            if (parameters != null)
            {
                var scriptPrams = new Dictionary<string, string>();
                foreach (var param in parameters)
                {
                    if (param.Value == null) continue;
                    var convertedValue = ConvertValue(param.Value, scriptPrams);
                    
                    declarations.AppendLine($"var {param.Key} = {convertedValue};");
                    if (scriptPrams.Count > 0)
                    {
                        foreach (var p in scriptPrams)
                        {
                            getFunctions.Append(p.Value);
                        }
                    }
                }
            }

            var scriptBody = expression.StartsWith('$') ? expression : $"$\"{expression}\"";
            if (!scriptBody.EndsWith(';'))
            {
                scriptBody += ";";
            }

            return getFunctions.ToString() + declarations.ToString() + $"return {scriptBody}";
        }

    }
}
