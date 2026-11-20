#!/usr/bin/env python3
"""Flattens the Lucide icons in ArtSource/Icons/lucide into polylines for the shell's UiIcons.

UiIcons draws its icons at runtime from these polylines, so the project needs no imported icon textures.
To add an icon, copy its SVG from https://lucide.dev into ArtSource/Icons/lucide and run:

    python3 ArtSource/Icons/build_icons.py

It rewrites Assets/InsightsSamples/_Common/UI/UiIconData.cs.
"""
import math
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent / "lucide"
OUTPUT = ROOT / "Assets/InsightsSamples/_Common/UI/UiIconData.cs"

# How far a flattened curve may stray from the true one, in units of Lucide's 24 x 24 grid.
TOLERANCE = 0.03
NUMBER = re.compile(r"[-+]?(?:\d*\.\d+|\d+\.?)(?:[eE][-+]?\d+)?")


def arc_steps(radius, sweep):
    if radius <= TOLERANCE:
        return 2
    step = 2 * math.acos(1 - TOLERANCE / radius)
    return max(2, math.ceil(abs(sweep) / step))


def ellipse_points(cx, cy, rx, ry, start, sweep, rotation=0.0):
    steps = arc_steps(max(rx, ry), sweep)
    cos_r, sin_r = math.cos(rotation), math.sin(rotation)
    points = []
    for i in range(1, steps + 1):
        angle = start + sweep * i / steps
        x, y = rx * math.cos(angle), ry * math.sin(angle)
        points.append((cx + cos_r * x - sin_r * y, cy + sin_r * x + cos_r * y))
    return points


def svg_arc(p0, rx, ry, rotation_degrees, large, sweep_flag, p1):
    """An SVG elliptical arc from p0 to p1 (endpoint form), as points after p0."""
    if rx == 0 or ry == 0 or p0 == p1:
        return [p1]
    rotation = math.radians(rotation_degrees)
    cos_r, sin_r = math.cos(rotation), math.sin(rotation)
    dx, dy = (p0[0] - p1[0]) / 2, (p0[1] - p1[1]) / 2
    x, y = cos_r * dx + sin_r * dy, -sin_r * dx + cos_r * dy
    rx, ry = abs(rx), abs(ry)
    scale = x * x / (rx * rx) + y * y / (ry * ry)
    if scale > 1:
        rx, ry = rx * math.sqrt(scale), ry * math.sqrt(scale)
    numerator = rx * rx * ry * ry - rx * rx * y * y - ry * ry * x * x
    denominator = rx * rx * y * y + ry * ry * x * x
    factor = math.sqrt(max(0.0, numerator / denominator)) * (1 if large != sweep_flag else -1)
    cxp, cyp = factor * rx * y / ry, -factor * ry * x / rx
    cx = cos_r * cxp - sin_r * cyp + (p0[0] + p1[0]) / 2
    cy = sin_r * cxp + cos_r * cyp + (p0[1] + p1[1]) / 2
    start = math.atan2((y - cyp) / ry, (x - cxp) / rx)
    end = math.atan2((-y - cyp) / ry, (-x - cxp) / rx)
    sweep = end - start
    if sweep_flag and sweep < 0:
        sweep += 2 * math.pi
    elif not sweep_flag and sweep > 0:
        sweep -= 2 * math.pi
    points = ellipse_points(cx, cy, rx, ry, start, sweep, rotation)
    points[-1] = p1
    return points


def bezier(points):
    """A quadratic or cubic Bezier through its control points, as points after the first."""
    length = sum(math.dist(points[i], points[i + 1]) for i in range(len(points) - 1))
    steps = max(4, math.ceil(length / 0.5))
    result = []
    for i in range(1, steps + 1):
        t = i / steps
        level = list(points)
        while len(level) > 1:
            level = [((1 - t) * a[0] + t * b[0], (1 - t) * a[1] + t * b[1]) for a, b in zip(level, level[1:])]
        result.append(level[0])
    return result


def flatten_path(d):
    polylines, current = [], None
    position, start, control = (0.0, 0.0), (0.0, 0.0), None
    index, command, previous = 0, None, None

    def skip():
        nonlocal index
        while index < len(d) and d[index] in " ,\t\r\n":
            index += 1

    def number():
        nonlocal index
        skip()
        match = NUMBER.match(d, index)
        if not match:
            raise ValueError(f"Expected a number at {index} in {d!r}")
        index = match.end()
        return float(match.group())

    def flag():
        nonlocal index
        skip()
        index += 1
        return d[index - 1] == "1"

    def point(relative):
        x, y = number(), number()
        return (position[0] + x, position[1] + y) if relative else (x, y)

    while True:
        skip()
        if index >= len(d):
            break
        if d[index].isalpha():
            command = d[index]
            index += 1
        elif command in "Mm":
            command = "L" if command == "M" else "l"
        relative, kind = command.islower(), command.upper()

        if kind == "Z":
            if current:
                current.append(start)
            position, control = start, None
        elif kind == "M":
            position = start = point(relative)
            current = [position]
            polylines.append(current)
            control = None
        else:
            if current is None or (previous == "Z"):
                current = [position]
                polylines.append(current)
            if kind == "L":
                position = point(relative)
                current.append(position)
            elif kind == "H":
                x = number()
                position = (position[0] + x if relative else x, position[1])
                current.append(position)
            elif kind == "V":
                y = number()
                position = (position[0], position[1] + y if relative else y)
                current.append(position)
            elif kind in "CS":
                if kind == "C":
                    first = point(relative)
                else:
                    first = (2 * position[0] - control[0], 2 * position[1] - control[1]) if control and previous in "CS" else position
                second, end = point(relative), point(relative)
                current.extend(bezier([position, first, second, end]))
                position, control = end, second
            elif kind in "QT":
                if kind == "Q":
                    first = point(relative)
                else:
                    first = (2 * position[0] - control[0], 2 * position[1] - control[1]) if control and previous in "QT" else position
                end = point(relative)
                current.extend(bezier([position, first, end]))
                position, control = end, first
            elif kind == "A":
                rx, ry, rotation = number(), number(), number()
                large, sweep = flag(), flag()
                end = point(relative)
                current.extend(svg_arc(position, rx, ry, rotation, large, sweep, end))
                position = end
            else:
                raise ValueError(f"Unsupported path command {command!r} in {d!r}")
            if kind not in "CSQT":
                control = None
        previous = kind
    return polylines


def flatten_rect(x, y, width, height, rx, ry):
    rx, ry = min(rx, width / 2), min(ry, height / 2)
    if rx == 0 or ry == 0:
        return [[(x, y), (x + width, y), (x + width, y + height), (x, y + height), (x, y)]]
    quarter = math.pi / 2
    points = [(x + rx, y), (x + width - rx, y)]
    points += ellipse_points(x + width - rx, y + ry, rx, ry, -quarter, quarter)
    points.append((x + width, y + height - ry))
    points += ellipse_points(x + width - rx, y + height - ry, rx, ry, 0, quarter)
    points.append((x + rx, y + height))
    points += ellipse_points(x + rx, y + height - ry, rx, ry, quarter, quarter)
    points.append((x, y + ry))
    points += ellipse_points(x + rx, y + ry, rx, ry, 2 * quarter, quarter)
    return [points]


def flatten_icon(path):
    polylines = []
    for element in ET.parse(path).getroot().iter():
        tag = element.tag.rsplit("}", 1)[-1]
        value = lambda name, default=0.0: float(element.get(name, default))
        if tag == "path":
            polylines += flatten_path(element.get("d"))
        elif tag == "rect":
            rx = value("rx", element.get("ry", 0))
            polylines += flatten_rect(value("x"), value("y"), value("width"), value("height"), rx, value("ry", rx))
        elif tag in ("circle", "ellipse"):
            rx, ry = (value("r"), value("r")) if tag == "circle" else (value("rx"), value("ry"))
            first = (value("cx") + rx, value("cy"))
            polylines.append([first] + ellipse_points(value("cx"), value("cy"), rx, ry, 0, 2 * math.pi))
        elif tag == "line":
            polylines.append([(value("x1"), value("y1")), (value("x2"), value("y2"))])
        elif tag in ("polyline", "polygon"):
            numbers = [float(n) for n in NUMBER.findall(element.get("points"))]
            points = list(zip(numbers[::2], numbers[1::2]))
            polylines.append(points + points[:1] if tag == "polygon" else points)
        elif tag != "svg":
            raise ValueError(f"{path.name}: unsupported element <{tag}>")
    return polylines


def encode(polylines):
    strokes = []
    for polyline in polylines:
        tenths = [(round(x * 10), round(y * 10)) for x, y in polyline]
        # Drop points that round onto the one before; a lone point stays, as Lucide's dots are zero-length lines.
        kept = [tenths[0]] + [b for a, b in zip(tenths, tenths[1:]) if a != b]
        strokes.append(" ".join(f"{x} {y}" for x, y in kept))
    return "/".join(strokes)


def main():
    icons = sorted(SOURCE.glob("*.svg"))
    if not icons:
        sys.exit(f"No SVGs in {SOURCE}")
    lines = [f'            "{icon.stem}", "{encode(flatten_icon(icon))}",' for icon in icons]
    OUTPUT.write_text(
        "// Built by ArtSource/Icons/build_icons.py from the Lucide icons in ArtSource/Icons/lucide\n"
        "// (ISC License, with the Feather-derived icons under MIT: see the LICENSE there).\n"
        "// Don't edit by hand: add the icon's SVG to that folder and run the script.\n"
        "namespace InsightsSamples.Common\n"
        "{\n"
        "    public static partial class UiIcons\n"
        "    {\n"
        "        // Name, then strokes: polylines on Lucide's 24 x 24 grid in tenths of a unit, separated by '/'.\n"
        "        private static readonly string[] Data =\n"
        "        {\n" + "\n".join(lines) + "\n"
        "        };\n"
        "    }\n"
        "}\n"
    )
    print(f"Wrote {len(icons)} icons to {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
