using System;
using System.Text.Json;
using System.Linq;

namespace MigracaoAD.UI;

public class Branding
{
    public string AppTitle { get; set; } = "Migração AD & Shares";
    public string DeveloperName { get; set; } = "SEU NOME";
    public string Year { get; set; } = "2025";
    public string Email { get; set; } = "";
    public string LinkedIn { get; set; } = "";
    public string GitHub { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string Tagline { get; set; } = "Transformando ideias em soluções digitais que fazem a diferença";

    public string Copyright => $"© {Year}";
    public string Contact => string.Join(" | ", new[]{LinkedIn, GitHub, Instagram, Email}.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static Branding Load()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "branding.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var b = JsonSerializer.Deserialize<Branding>(json, new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
                if (b != null) return b;
            }
        }
        catch { }
        return new Branding();
    }
}

