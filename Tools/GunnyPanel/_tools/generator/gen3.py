# -*- coding: utf-8 -*-
"""Generator part 3/3: patch cac diem de compile + restore/build solution."""
import os
import subprocess

ROOT = r"C:\Gunny\ToolGunny\GunnyPanel"


def read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
        return f.read()


def write(rel, content):
    path = os.path.join(ROOT, rel)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)
    print("PATCHED:", rel)


def patch(rel, func):
    old = read(rel)
    new = func(old)
    if new == old:
        print("NO-CHANGE:", rel)
        return
    write(rel, new)


def fix_program(s):
    if "using Microsoft.AspNetCore.Mvc.RazorPages;" not in s:
        s = s.replace(
            "using Microsoft.AspNetCore.Authentication.Cookies;",
            "using Microsoft.AspNetCore.Authentication.Cookies;\nusing Microsoft.AspNetCore.Mvc.RazorPages;",
        )
    old = "builder.Services.AddRazorPages();"
    new = """builder.Services.AddRazorPages()
    .AddRazorPagesOptions(o =>
    {
        o.Conventions.AuthorizeFolder("/");
        o.Conventions.AllowAnonymousToPage("/Login");
    });"""
    return s.replace(old, new)


def fix_viewimports(s):
    if "@addTagHelper" in s:
        return s
    return s.rstrip() + "\n@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers\n"


def fix_layout(s):
    if "@inject Microsoft.Extensions.Configuration.IConfiguration Config" not in s:
        s = s.replace("@{\n", "@inject Microsoft.Extensions.Configuration.IConfiguration Config\n@{\n", 1)
    old_link = (
        '<a class="ext" href="@(Context.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) '
        'is Microsoft.Extensions.Configuration.IConfiguration c ? (c["Vng:AdmincpUrl"] ?? "http://127.0.0.1:90/Users") '
        ': "http://127.0.0.1:90/Users")" target="_blank" rel="noopener">Admincp cũ ↗</a>'
    )
    new_link = (
        '<a class="ext" href="@(Config["Vng:AdmincpUrl"] ?? "http://127.0.0.1:90/Users")" '
        'target="_blank" rel="noopener">Admincp cũ ↗</a>'
    )
    return s.replace(old_link, new_link)


def fix_jobs(s):
    old = '<input type="checkbox" name="SyncOverwrite" value="true" checked="@Model.SyncOverwrite" />'
    new = '<input type="checkbox" name="SyncOverwrite" value="true" @(Model.SyncOverwrite ? "checked" : null) />'
    return s.replace(old, new)


def fix_db(s):
    old = '<option value="@name" selected="@(name == Model.Connection)">@name</option>'
    new = '<option value="@name" @(name == Model.Connection ? "selected" : null)>@name</option>'
    return s.replace(old, new)


def run(cmd, cwd, timeout=600):
    print("RUN:", " ".join(cmd))
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
    out = (p.stdout or "").strip()
    err = (p.stderr or "").strip()
    if out:
        print(out[-4000:])
    if err:
        print("STDERR:", err[-6000:])
    if p.returncode != 0:
        raise SystemExit(f"THAT BAI: {cmd} (code {p.returncode})")


if __name__ == "__main__":
    patch(r"src\GunnyPanel\Program.cs", fix_program)
    patch(r"src\GunnyPanel\Pages\_ViewImports.cshtml", fix_viewimports)
    patch(r"src\GunnyPanel\Pages\Shared\_Layout.cshtml", fix_layout)
    patch(r"src\GunnyPanel\Pages\Jobs.cshtml", fix_jobs)
    patch(r"src\GunnyPanel\Pages\Db.cshtml", fix_db)

    run(["dotnet", "restore", "GunnyPanel.sln"], ROOT, timeout=600)
    run(["dotnet", "build", "GunnyPanel.sln", "-c", "Debug", "--nologo"], ROOT, timeout=600)
    print("BUILD OK")
