# -*- coding: utf-8 -*-
"""Gen4: fix Razor RZ1031 (tag helper attribute) + rebuild."""
import os
import subprocess

ROOT = r"C:\Gunny\ToolGunny\GunnyPanel"


def patch(rel, old, new):
    path = os.path.join(ROOT, rel)
    with open(path, encoding="utf-8") as f:
        s = f.read()
    if old not in s:
        print("NO-CHANGE:", rel)
        return
    if "?" in new and False:
        pass
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(s.replace(old, new))
    print("PATCHED:", rel)


OLD_OPTION = '''                <select name="Connection">
                    @foreach (var name in Model.Connections)
                    {
                        <option value="@name" @(name == Model.Connection ? "selected" : null)>@name</option>
                    }
                </select>'''

NEW_OPTION = '''                <select name="Connection">
                    @foreach (var name in Model.Connections)
                    {
                        if (name == Model.Connection)
                        {
                            <option value="@name" selected>@name</option>
                        }
                        else
                        {
                            <option value="@name">@name</option>
                        }
                    }
                </select>'''

OLD_CHECK = '<input type="checkbox" name="SyncOverwrite" value="true" @(Model.SyncOverwrite ? "checked" : null) />'
NEW_CHECK = '<input type="checkbox" name="SyncOverwrite" value="true" checked="@Model.SyncOverwrite" />'


def run(cmd, cwd, timeout=600):
    print("RUN:", " ".join(cmd))
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
    if (p.stdout or "").strip():
        print(p.stdout.strip()[-4000:])
    if (p.stderr or "").strip():
        print("STDERR:", p.stderr.strip()[-6000:])
    if p.returncode != 0:
        raise SystemExit(f"THAT BAI: {cmd} (code {p.returncode})")


if __name__ == "__main__":
    patch(r"src\GunnyPanel\Pages\Db.cshtml", OLD_OPTION, NEW_OPTION)
    patch(r"src\GunnyPanel\Pages\Jobs.cshtml", OLD_CHECK, NEW_CHECK)

    slnx = os.path.join(ROOT, "GunnyPanel.slnx")
    if os.path.exists(slnx):
        os.remove(slnx)
        print("REMOVED: GunnyPanel.slnx (giu GunnyPanel.sln)")

    run(["dotnet", "build", r"src\GunnyPanel\GunnyPanel.csproj", "-c", "Debug", "--nologo"], ROOT, timeout=600)
    print("BUILD OK")
