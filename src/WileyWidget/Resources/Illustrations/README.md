# Empty State Illustrations

This directory contains illustrations for empty state UI components.

## Required Illustrations

Based on the UI Polish Audit Report (Issue #4), the following illustrations are needed:

### 1. empty_enterprises.svg (Priority 1)

- **Usage**: Empty state for Enterprise distribution section in Dashboard
- **Size**: 200×200 px
- **Style**: Minimalist, line art, monochrome
- **Theme**: Building/organization icon with "empty" indication

### 2. empty_chart.svg (Priority 1)

- **Usage**: Empty state for Budget Trends chart section
- **Size**: 200×200 px
- **Style**: Minimalist, line art, monochrome
- **Theme**: Bar chart or line graph with "no data" indication

### 3. empty_activity.svg (Priority 1)

- **Usage**: Empty state for Recent Activity section
- **Size**: 200×200 px
- **Style**: Minimalist, line art, monochrome
- **Theme**: Clock or calendar icon with "no activity" indication

## Recommended Sources

1. **unDraw** (Open source, customizable)
   - https://undraw.co/
   - Search for: "empty", "no data", "void", "empty state"
   - Customizable colors (use #2196F3 for consistency)

2. **Storyset** (Free with attribution)
   - https://storyset.com/
   - Great for business/data illustrations
   - Available in various styles

3. **Humaaans** (CC BY 4.0)
   - https://www.humaaans.com/
   - Mix-and-match characters and objects
   - Professional appearance

## Style Guidelines

- **Format**: SVG (vector) or high-DPI PNG (400×400 at 2x resolution)
- **Colors**: Monochrome with #2196F3 accent
- **Background**: Transparent
- **Style**: Line art, minimalist, consistent with FluentDark theme
- **Viewbox**: 0 0 200 200 for SVG files

## Usage Example

```xml
<StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" MaxWidth="400">
    <!-- Illustration -->
    <Image Source="/src/Resources/Illustrations/empty_enterprises.svg"
           Width="200" Height="200" Margin="0,0,0,24" />

    <!-- Primary message -->
    <TextBlock Text="No Enterprise Data Yet"
               FontSize="20" FontWeight="Bold"
               Foreground="White" HorizontalAlignment="Center" />

    <!-- Descriptive text -->
    <TextBlock Text="Get started by creating your first enterprise fund to track municipal utility operations."
               FontSize="14" Foreground="#B0B0B0" TextWrapping="Wrap"
               HorizontalAlignment="Center" Margin="0,12,0,24"
               TextAlignment="Center" />

    <!-- Call to action -->
    <syncfusion:ButtonAdv Content="Create First Enterprise"
                          Command="{Binding AddEnterpriseCommand}"
                          SizeMode="Large" Width="200" Height="44" />
</StackPanel>
```

## Implementation Notes

- SVG files should be added to the project with Build Action: Resource
- For PNG files, use 2x resolution (400×400) for high-DPI displays
- Consider creating dark theme variants if illustrations contain backgrounds
- Test illustrations on both dark and light backgrounds

## Attribution

If using illustrations from sources requiring attribution:

- Add attribution in the About dialog
- Include license files in the licenses/ directory
- Document source URLs in this README

## Color Palette

Match the Wiley Widget theme:

- Primary Accent: #2196F3 (AccentBlueBrush)
- Success: #4CAF50 (AccentGreenBrush)
- Background (for reference): #1E1E1E (CardBackground)
- Secondary Text: #B0B0B0 (SecondaryTextBrush2)
- Tertiary Text: #808080 (TertiaryTextBrush)
