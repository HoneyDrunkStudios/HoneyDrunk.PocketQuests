namespace PocketQuests.DevelopmentHost;

internal static class DevelopmentProxy
{
    internal static async Task Forward(HttpContext context, HttpClient client, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path);
        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);
            if (context.Request.ContentType is { } contentType)
                request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }

        if (context.Request.Headers.Authorization.Count > 0)
            request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers.Authorization.ToArray());
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.Headers.CacheControl = "no-store";
        foreach (var header in new[] { "Content-Type", "Content-Disposition", "Retry-After", "WWW-Authenticate" })
        {
            if (response.Headers.TryGetValues(header, out var values) || response.Content.Headers.TryGetValues(header, out values))
                context.Response.Headers[header] = values.ToArray();
        }

        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}
