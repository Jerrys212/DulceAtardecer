namespace DulceAtardecer.Constants;

public static class SwaggerUiPage
{
    public const string Html = """
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>DulceAtardecer API</title>
          <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.17.14/swagger-ui.css">
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.17.14/swagger-ui-bundle.js"></script>
          <script>
            window.ui = SwaggerUIBundle({ url: "/openapi/v1.json", dom_id: "#swagger-ui", persistAuthorization: true });
          </script>
        </body>
        </html>
        """;
}
