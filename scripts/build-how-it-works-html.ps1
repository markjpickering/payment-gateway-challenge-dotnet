# Builds docs/how-it-works.html, a standalone copy of docs/how-it-works.md that opens in any browser (GitHub only shows an
# HTML file's source). Run it after editing the Markdown; HowItWorksPageTests fails if the page is out of date.
# The page embeds the Markdown and draws it in the browser with two pinned scripts loaded from a CDN, so viewing it needs
# an internet connection.
# Usage: pwsh scripts/build-how-it-works-html.ps1

$docs = Join-Path $PSScriptRoot '..\docs'
$markdown = (Get-Content (Join-Path $docs 'how-it-works.md') -Raw -Encoding utf8).Replace("`r`n", "`n").TrimEnd()

if ($markdown -match '</script') { throw 'how-it-works.md must not contain "</script": it is embedded in a script element.' }

$template = @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>How the Payment Gateway works</title>
<style>
  :root { --bg: #ffffff; --fg: #1f2328; --muted: #59636e; --line: #d1d9e0; --code: #f6f8fa; --link: #0969da; }
  @media (prefers-color-scheme: dark) { :root { --bg: #0d1117; --fg: #e6edf3; --muted: #9198a1; --line: #3d444d; --code: #151b23; --link: #4493f8; } }
  html { background: var(--bg); }
  body { margin: 0; color: var(--fg); font: 16px/1.6 system-ui, -apple-system, "Segoe UI", sans-serif; }
  main { max-width: 90rem; margin: 0 auto; padding: 2rem 1rem 4rem; }
  p, ul, ol, h1, h2, h3 { max-width: 62rem; }
  h1, h2, h3 { line-height: 1.25; } h2 { margin-top: 2.2rem; padding-bottom: .3rem; border-bottom: 1px solid var(--line); }
  a { color: var(--link); } code { background: var(--code); padding: .1em .35em; border-radius: 4px; font-size: .9em; }
  table { border-collapse: collapse; display: block; overflow-x: auto; } th, td { border: 1px solid var(--line); padding: .4rem .75rem; text-align: left; vertical-align: top; }
  pre.mermaid { background: transparent; text-align: center; overflow-x: auto; margin: 1.5rem 0; }
  .note { color: var(--muted); }
</style>
</head>
<body>
<main id="content"><p class="note">Loading the diagrams&hellip; this page loads two small scripts from a CDN, so it needs an internet connection.</p></main>
<noscript><main><p>This page needs JavaScript to draw its diagrams. The same content is in how-it-works.md.</p></main></noscript>

<script id="source" type="text/markdown">
{{MARKDOWN}}
</script>
<script src="https://cdn.jsdelivr.net/npm/marked@15.0.4/marked.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.min.js"></script>
<script>
  const root = document.getElementById('content');
  root.innerHTML = marked.parse(document.getElementById('source').textContent);
  root.querySelectorAll('pre > code.language-mermaid').forEach(function (code) {
    const pre = document.createElement('pre');
    pre.className = 'mermaid';
    pre.textContent = code.textContent;
    code.parentElement.replaceWith(pre);
  });
  const dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  mermaid.initialize({ startOnLoad: false, theme: dark ? 'dark' : 'default', securityLevel: 'strict', sequence: { useMaxWidth: false } });
  mermaid.run({ querySelector: 'pre.mermaid' });
</script>
</body>
</html>
'@

$html = $template.Replace('{{MARKDOWN}}', $markdown)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $docs 'how-it-works.html'), $html.Replace("`r`n", "`n"), $utf8NoBom)
"Wrote docs/how-it-works.html ($($html.Length) characters)."
