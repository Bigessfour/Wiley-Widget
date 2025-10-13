# DashboardView Visual Layout

## Overall Layout Structure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Wiley Widget Dashboard - Municipal Financial Overview                      │
├─────────────────────────────────────────────────────────────────────────────┤
│  ▼ Dashboard Tab                                           [Refresh] [Export]│
│     Actions                View                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  ████████████ (Loading Progress Bar - 4px height)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Key Performance Indicators                                                 │
│  ┌────────────────┐ ┌────────────────┐ ┌────────────────┐ ┌──────────────┐│
│  │Budget Utilization│ │ System Health  │ │Total Enterprises│ │Active Projects│
│  │                 │ │                │ │                │ │              ││
│  │    ╭───╮       │ │    ╭───╮       │ │    ╭───╮       │ │   ╭───╮     ││
│  │   ╱  ↗ ╲      │ │   ╱  ↗ ╲      │ │   ╱█████╲      │ │  ╱  △ ╲    ││
│  │  │  78% │     │ │  │  95% │     │ │  │█████││     │ │ │  15  │   ││
│  │   ╲     ╱      │ │   ╲     ╱      │ │   ╲█████╱      │ │  ╲     ╱    ││
│  │    ╰───╯       │ │    ╰───╯       │ │    ╰───╯       │ │   ╰───╯     ││
│  │                 │ │                │ │                │ │              ││
│  │      78%        │ │   Excellent    │ │       42       │ │      15      ││
│  │   Total: $2.5M  │ │   95% Score    │ │  +2 from last  │ │ +1 from week ││
│  └────────────────┘ └────────────────┘ └────────────────┘ └──────────────┘│
│                                                                              │
│  Financial Trends and Analytics                                             │
│  ┌─────────────────────────────────────────────┐ ┌──────────────────────┐  │
│  │ Budget Trends (Historical Data)             │ │ Enterprise Distribution│
│  │                                              │ │                      │  │
│  │  $3M ─                                       │ │   ╭─────╮  Water 40% │  │
│  │       ╲                                      │ │  ╱       ╲           │  │
│  │  $2M ─ ╲   ╱───╲  ╱─────                    │ │ │  Sewer  │ Sewer 30%│  │
│  │         ╲ ╱     ╲╱                           │ │ │   ╲   ╱            │  │
│  │  $1M ─  ╲╱                                   │ │  ╲  Gas╱  Gas 20%    │  │
│  │          └┴─┴─┴─┴─┴─┴                        │ │   ╰───╯   Other 10%  │  │
│  │         May Jun Jul Aug Sep Oct              │ │                      │  │
│  │                                              │ │                      │  │
│  │  [Hover for details - Zoom/Pan enabled]     │ │                      │  │
│  └─────────────────────────────────────────────┘ └──────────────────────┘  │
│                                                                              │
│  System Alerts and Recent Activity                                          │
│  ┌────────────────────────────────────────┐ ┌───────────────────────────┐  │
│  │ Critical Alerts (Over-Budget Warnings) │ │ Recent Activity           │  │
│  │ ┌──────────────────────────────────────┤ │┌─────────────────────────┤  │
│  │ │Priority │ Alert Message      │Time  ││ ││Time │Activity  │Type    ││  │
│  │ ├──────────────────────────────────────┤ │├─────────────────────────┤  │
│  │ │🔴 High  │ Budget exceeded    │14:23 ││ ││14:30│Budget up │Budget  ││  │
│  │ │🟡 Medium│ Maintenance due    │13:45 ││ ││14:15│New enter │Enterpr ││  │
│  │ │🔵 Low   │ Backup scheduled   │12:00 ││ ││14:00│Report gen│Report  ││  │
│  │ │🔴 High  │ Over 80% utilized  │11:30 ││ ││13:45│DB backup │System  ││  │
│  │ │🟡 Medium│ Review needed      │10:15 ││ ││13:30│Settings  │Config  ││  │
│  │ └──────────────────────────────────────┘ │└─────────────────────────┘  │
│  └────────────────────────────────────────┘ └───────────────────────────┘  │
│                                                                              │
│  Quick Navigation                                                            │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐              │
│  │   Enterprise    │ │     Budget      │ │    Generate     │              │
│  │   Management    │ │    Analysis     │ │     Report      │ ...          │
│  └─────────────────┘ └─────────────────┘ └─────────────────┘              │
│                                                                              │
├─────────────────────────────────────────────────────────────────────────────┤
│ Last Updated: 14:32:05 | ⟳ Loading... | Status: Ready | Next: 14:37       │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Color-Coded Sections

### Top Section - KPIs (4 Gauges)
```
┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│   BLUE   │  │  GREEN   │  │  PURPLE  │  │  ORANGE  │
│  Budget  │  │  Health  │  │Enterprise│  │ Projects │
│  Gauge   │  │  Gauge   │  │  Gauge   │  │  Gauge   │
└──────────┘  └──────────┘  └──────────┘  └──────────┘
```

### Middle Section - Charts
```
┌────────────────────────────┐  ┌─────────────┐
│         BLUE               │  │   PURPLE    │
│     Budget Trend Chart     │  │  Pie Chart  │
│  (Line with Trackball)     │  │(Distribution)│
└────────────────────────────┘  └─────────────┘
```

### Bottom Section - Grids
```
┌──────────────────┐  ┌──────────────────┐
│      RED         │  │     GREEN        │
│  Alerts Grid     │  │  Activity Grid   │
│ (Formatted Cells)│  │  (Time-sorted)   │
└──────────────────┘  └──────────────────┘
```

## Gauge Detail Views

### Budget Utilization Gauge (Needle Type)
```
        ┌─────────────────┐
        │ Budget Utilization│
        └─────────────────┘
             ╭───────╮
          100│       │0
         ╱   │   ↗   │   ╲
        │ 80 │  78%  │ 20 │
        │    │       │    │
         ╲ 60│       │40 ╱
          ╲  │       │  ╱
           ╲ └───────┘ ╱
            ╲    █    ╱
             ╲───█───╱
                 █
            [Legend]
         Green: 0-60%
        Yellow: 60-80%
          Red: 80-100%
```

### System Health Gauge (Needle Type)
```
        ┌─────────────────┐
        │  System Health   │
        └─────────────────┘
             ╭───────╮
          100│       │0
         ╱   │   ↗   │   ╲
        │ 75 │  95%  │ 25 │
        │    │       │    │
         ╲ 50│       │50 ╱
          ╲  │       │  ╱
           ╲ └───────┘ ╱
            ╲    █    ╱
             ╲───█───╱
                 █
            [Legend]
          Red: 0-50%
        Yellow: 50-75%
         Green: 75-100%
```

### Enterprise Count Gauge (Range Type)
```
        ┌─────────────────┐
        │ Total Enterprises│
        └─────────────────┘
             ╭───────╮
          100│███████│0
         ╱   │███████│   ╲
        │ 80 │███████│ 20 │
        │    │███42██│    │
         ╲ 60│███████│40 ╱
          ╲  │███████│  ╱
           ╲ └───────┘ ╱
            ╲─────────╱
            
              42
         +2 from last month
```

### Active Projects Gauge (Symbol Type)
```
        ┌─────────────────┐
        │  Active Projects │
        └─────────────────┘
             ╭───────╮
           50│       │0
         ╱   │   △   │   ╲
        │ 40 │  15   │ 10 │
        │    │       │    │
         ╲ 30│       │20 ╱
          ╲  │       │  ╱
           ╲ └───────┘ ╱
            ╲─────────╱
            
              15
         +1 from last week
```

## Chart Detail Views

### Budget Trend Chart (with Trackball)
```
┌──────────────────────────────────────────────┐
│ 6-Month Budget Trend                         │
│                                              │
│ $3.0M ┐                                      │
│       │                                      │
│ $2.5M ┤     ●───●                            │
│       │    ╱     ╲                           │
│ $2.0M ┤   ●       ●───●                      │
│       │  ╱             ╲                     │
│ $1.5M ┤ ●               ●                    │
│       │                                      │
│ $1.0M └─┬───┬───┬───┬───┬───┬               │
│         May Jun Jul Aug Sep Oct              │
│                                              │
│  [Hover to see trackball tooltip]           │
│  [Mouse wheel to zoom]                      │
│  [Click and drag to pan]                    │
└──────────────────────────────────────────────┘

Trackball Tooltip (appears on hover):
┌──────────────────────────┐
│ Jul 2025                 │
│ Amount: $2,450,000       │
│ Hover for details (data  │
│ from municipal accounts) │
└──────────────────────────┘
```

### Enterprise Distribution Pie Chart
```
┌────────────────────────────┐
│                            │
│         ╱─────╮            │
│        ╱ Water ╲           │
│       │   40%   │          │
│       ╲─────────╱╮         │
│               ╱╱  ╲        │
│         Sewer│30% │        │
│               ╲   ╱        │
│           ╱────╲╱╮         │
│          │ Gas  │╲         │
│          │ 20%  │ ╲        │
│           ╲─────╱  Other   │
│                    10%     │
│                            │
│  Legend:                   │
│  ● Water   ● Sewer         │
│  ● Gas     ● Other         │
└────────────────────────────┘
```

## Alert Grid Detail

```
┌───────────────────────────────────────────────────────┐
│ Critical Alerts (Over-Budget Warnings)                │
├───────────┬─────────────────────────┬─────────────────┤
│ Priority  │ Alert Message           │ Time            │
├───────────┼─────────────────────────┼─────────────────┤
│ 🔴 High   │ Budget exceeded for     │ 14:23:45        │
│           │ Water Department        │                 │
├───────────┼─────────────────────────┼─────────────────┤
│ 🟡 Medium │ Maintenance scheduled   │ 13:45:12        │
│           │ for next week           │                 │
├───────────┼─────────────────────────┼─────────────────┤
│ 🔵 Low    │ Backup scheduled for    │ 12:00:00        │
│           │ tonight                 │                 │
├───────────┼─────────────────────────┼─────────────────┤
│ 🔴 High   │ Over 80% budget         │ 11:30:22        │
│           │ utilization detected    │                 │
├───────────┼─────────────────────────┼─────────────────┤
│ 🟡 Medium │ Quarterly review        │ 10:15:33        │
│           │ required                │                 │
└───────────┴─────────────────────────┴─────────────────┘

Cell Formatting Rules:
- High Priority: RED (#F44336), Bold
- Medium Priority: YELLOW (#FFC107), SemiBold
- Low Priority: BLUE (#2196F3), Normal
```

## Navigation Buttons Layout

```
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│                 │ │                 │ │                 │
│   Enterprise    │ │     Budget      │ │    Generate     │
│   Management    │ │    Analysis     │ │     Report      │
│                 │ │                 │ │                 │
└─────────────────┘ └─────────────────┘ └─────────────────┘

┌─────────────────┐ ┌─────────────────┐
│                 │ │                 │
│    Settings     │ │   Backup Data   │
│                 │ │                 │
└─────────────────┘ └─────────────────┘

Size: 180px × 60px each
Layout: WrapPanel (responsive)
Tooltips: Show on hover
```

## Responsive Behavior

### Full Window (1400px width)
```
[Gauge1] [Gauge2] [Gauge3] [Gauge4]
[       Chart      ] [Pie]
[  Alerts Grid    ] [ Activity ]
[Btn1][Btn2][Btn3][Btn4][Btn5]
```

### Medium Window (1000px width)
```
[Gauge1] [Gauge2] [Gauge3] [Gauge4]
[       Chart      ]
[       Pie        ]
[  Alerts Grid    ]
[  Activity Grid  ]
[Btn1][Btn2][Btn3]
[Btn4][Btn5]
```

### Small Window (800px width - with scrolling)
```
[Gauge1] [Gauge2]
[Gauge3] [Gauge4]
[     Chart     ]
[      Pie      ]
[  Alerts Grid  ]
[ Activity Grid ]
[Btn1][Btn2]
[Btn3][Btn4]
[Btn5]
```

## Animation Sequences

### On Load (Time-based)
```
0ms:    Window appears with theme
200ms:  Gauges fade in
400ms:  Gauge needles animate to values (1500ms duration)
600ms:  Charts fade in
800ms:  Chart series draw with animation (1000ms)
1000ms: Grids populate with data
1200ms: Buttons become interactive
```

### On Refresh
```
0ms:    Loading indicator appears
100ms:  Gauge values update (animate to new values)
200ms:  Chart data updates (smooth transition)
300ms:  Grids refresh
400ms:  Loading indicator disappears
```

### On Hover (Chart)
```
0ms:    Mouse enters chart area
50ms:   Trackball line appears
100ms:  Tooltip fades in
---:    Tooltip follows mouse
100ms:  Tooltip fades out on mouse leave
```

## Theme Comparison

### FluentDark (Primary)
```
Background: #1E1E1E (Dark gray)
Foreground: #FFFFFF (White)
Accents: Bright blues, greens, purples
Borders: Colored (#2196F3, #4CAF50, etc.)
Grid: Dark with colored headers
```

### Fluent Light (Fallback)
```
Background: #FFFFFF (White)
Foreground: #000000 (Black)
Accents: Same bright colors
Borders: Same colored borders
Grid: Light with colored headers
```

## Accessibility Features

### High Contrast Considerations
- All text maintains 4.5:1 contrast ratio minimum
- Color coding supplemented with icons (🔴🟡🔵)
- Tooltips provide text alternatives
- Keyboard navigation supported

### Screen Reader Support
- All gauges have accessible names
- Chart data accessible via data tables
- Button tooltips provide context
- Grid cells have proper ARIA labels

---

**Note**: This is a conceptual ASCII representation. The actual dashboard uses Syncfusion's professional WPF controls with smooth gradients, anti-aliasing, and modern visual effects.

**Theme**: FluentDark with colored accents
**Resolution**: Optimized for 1400×800 and above
**Animations**: Smooth 60fps transitions
**Interactivity**: Full mouse and keyboard support
