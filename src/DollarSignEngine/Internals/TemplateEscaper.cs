namespace DollarSignEngine.Internals;

internal static class TemplateEscaper
{
    private static string OPEN = "@@OPEN@@";
    private static string CLOSE = "@@CLOSE@@";

    public static string EscapeBlocks(string template) // 새로운 로직으로 대체
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        // 1단계: "{{" 를 "@@OPEN@@" 으로 정방향 치환
        System.Text.StringBuilder pass1Builder = new System.Text.StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            if (i <= template.Length - 2 && template[i] == '{' && template[i + 1] == '{')
            {
                pass1Builder.Append(OPEN);
                i += 2; // "{{" 두 글자 건너뛰기
            }
            else
            {
                pass1Builder.Append(template[i]);
                i++;
            }
        }
        string intermediateResult = pass1Builder.ToString();

        // 2단계: "}}" 를 "@@CLOSE@@" 으로 역방향 치환
        // 역방향 탐색 및 치환은 StringBuilder.Insert(0, ...)를 사용하여 구현
        System.Text.StringBuilder finalBuilder = new System.Text.StringBuilder();
        int j = intermediateResult.Length - 1;
        while (j >= 0)
        {
            // 현재 위치 j와 그 앞의 j-1 위치를 확인하여 "}}" 패턴을 찾음
            if (j > 0 && intermediateResult[j - 1] == '}' && intermediateResult[j] == '}')
            {
                finalBuilder.Insert(0, CLOSE); // 결과의 맨 앞에 CLOSE 추가
                j -= 2; // "}}" 두 글자 건너뛰기 (인덱스를 2만큼 앞으로 이동)
            }
            else
            {
                finalBuilder.Insert(0, intermediateResult[j]); // 결과의 맨 앞에 현재 문자 추가
                j--;
            }
        }
        return finalBuilder.ToString();
    }

    public static string UnescapeBlocks(string template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var result = new System.Text.StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            if (i <= template.Length - OPEN.Length && template.Substring(i, OPEN.Length) == OPEN)
            {
                result.Append("{");
                i += OPEN.Length; // Skip @@OPEN@@
            }
            else if (i <= template.Length - CLOSE.Length && template.Substring(i, CLOSE.Length) == CLOSE)
            {
                result.Append("}");
                i += CLOSE.Length; // Skip @@CLOSE@@
            }
            else
            {
                result.Append(template[i]);
                i++;
            }
        }
        return result.ToString();
    }
}
