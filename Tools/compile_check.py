"""Compile runtime/editor scripts with Unity-generated projects; fail on every build error."""
import glob
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent


def with_missing(csproj, folders, anchor):
    source = (ROOT / csproj).read_text(encoding="utf-8-sig")
    have = {p.replace("/", "\\") for p in re.findall(r'<Compile Include="([^"]+)"', source)}
    adds = []
    for folder in folders:
        for file in sorted((ROOT / folder).glob("*.cs")):
            relative = str(file.relative_to(ROOT)).replace("/", "\\")
            if relative not in have:
                adds.append(relative)
    if anchor not in source:
        raise ValueError(f"anchor not found in {csproj}; regenerate Unity project files")
    extra = "".join(f'\n    <Compile Include="{path}" />' for path in adds)
    return source.replace(anchor, anchor + extra, 1), adds


def build(path):
    result = subprocess.run(
        ["dotnet", "build", path, "-nologo", "-v", "m", "-t:Rebuild"],
        cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace"
    )
    output = result.stdout + result.stderr
    # MSBuild, restore and SDK failures are just as fatal as C# compiler errors.
    if result.returncode:
        print(output)
    return result.returncode


def main():
    temporary = [ROOT / "_tmp_runtime.csproj", ROOT / "_tmp_editor.csproj"]
    if any(path.exists() for path in temporary):
        print("Temporary compile projects already exist; refusing to overwrite.", file=sys.stderr)
        return 1
    try:
        runtime, runtime_adds = with_missing(
            "Assembly-CSharp.csproj", ["Assets/Scripts/Journey"],
            '    <Compile Include="Assets\\Scripts\\Journey\\OrganStage.cs" />')
        editor, editor_adds = with_missing(
            "Assembly-CSharp-Editor.csproj", ["Assets/Scripts/Journey/Editor"],
            '    <Compile Include="Assets\\Scripts\\Journey\\Editor\\JourneyBuilder.cs" />')
        editor = editor.replace('<ProjectReference Include="Assembly-CSharp.csproj">',
                                '<ProjectReference Include="_tmp_runtime.csproj">')
        temporary[0].write_text(runtime, encoding="utf-8-sig")
        temporary[1].write_text(editor, encoding="utf-8-sig")
        runtime_status = build(temporary[0].name)
        editor_status = build(temporary[1].name)
        if runtime_adds or editor_adds:
            print("Added unregistered sources:", runtime_adds + editor_adds)
        print(f"RUNTIME: {'PASS' if runtime_status == 0 else 'FAIL'} | EDITOR: {'PASS' if editor_status == 0 else 'FAIL'}")
        return int(runtime_status != 0 or editor_status != 0)
    except (OSError, ValueError) as exc:
        print(f"Compile check failed: {exc}", file=sys.stderr)
        return 1
    finally:
        for path in temporary:
            path.unlink(missing_ok=True)


if __name__ == "__main__":
    sys.exit(main())
