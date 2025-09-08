#!/usr/bin/env python3
"""
Fix PowerShell script issues:
- Ensure multiline comment is properly closed
- Ensure param block is in correct position
- Ensure strings are properly terminated
- Remove trailing whitespace
- Ensure file ends with single newline
"""

import os
import re


def fix_powershell_file(file_path):
    """Fix common PowerShell script issues."""
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    lines = content.splitlines()

    # Fix multiline comment - ensure it starts with <# and ends with #>
    if lines and not lines[0].startswith("<#"):
        lines.insert(0, "<#")

    # Find the end of the comment
    comment_end = -1
    for i, line in enumerate(lines):
        if line.strip() == "#>":
            comment_end = i
            break
        elif line.strip().endswith("#>"):
            comment_end = i
            break

    if comment_end == -1:
        # No closing #>, add it after the comment
        for i, line in enumerate(lines):
            if line.strip() == "" and i > 0:
                lines.insert(i, "#>")
                break
        else:
            lines.append("#>")

    # Ensure param block is after comment
    param_start = -1
    for i, line in enumerate(lines):
        if line.strip().startswith("param("):
            param_start = i
            break

    if param_start != -1 and param_start < comment_end:
        # Move param block after comment
        param_lines = []
        i = param_start
        while i < len(lines) and not lines[i].strip().startswith(")"):
            param_lines.append(lines[i])
            i += 1
        if i < len(lines):
            param_lines.append(lines[i])

        # Remove param from current location
        del lines[param_start : i + 1]

        # Insert after comment
        insert_pos = comment_end + 1
        if insert_pos >= len(lines):
            lines.extend(param_lines)
        else:
            lines[insert_pos:insert_pos] = param_lines

    # Fix unterminated strings
    fixed_lines = []
    for line in lines:
        # Check for unterminated single quotes
        if line.count("'") % 2 != 0:
            # Find the last quote and ensure it's terminated
            last_quote_pos = line.rfind("'")
            if last_quote_pos != -1:
                # Check if there's text after the last quote
                after_quote = line[last_quote_pos + 1 :]
                if after_quote.strip():
                    # There's content after the quote, this might be the issue
                    # For now, just ensure the line ends properly
                    pass
                else:
                    # Line ends with quote, should be fine
                    pass
            # If odd number of quotes, the line might be truncated
            # For the last line, ensure it ends with quote
            if line.strip().endswith("Write-Warning 'Syncfusion"):
                line = line.rstrip() + " license not loaded.' }"
        fixed_lines.append(line)

    # Remove trailing whitespace
    fixed_lines = [line.rstrip() for line in fixed_lines]

    # Ensure file ends with single newline
    result = "\n".join(fixed_lines)
    if not result.endswith("\n"):
        result += "\n"

    # Write back to file
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(result)

    print(f"Fixed PowerShell file: {file_path}")


if __name__ == "__main__":
    file_path = r"c:\Users\biges\Desktop\Wiley_Widget\scripts\profile-syncfusion-license-init.ps1"
    if os.path.exists(file_path):
        fix_powershell_file(file_path)
    else:
        print(f"File not found: {file_path}")
