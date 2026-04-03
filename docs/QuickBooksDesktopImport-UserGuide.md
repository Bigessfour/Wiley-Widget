# QuickBooks Desktop Import User Guide

Audience: town clerk / finance staff
Last updated: 2026-03-18

## What This Guide Does

This guide shows how to export supported data from QuickBooks Desktop on your workstation and prepare it for import into Wiley Widget. Wiley Widget imports exported files; it does not open the QuickBooks company file directly.

## Before You Start

1. Sign in to the QuickBooks Desktop company file you normally use.
2. Confirm you can save files to a folder you can reach later from Wiley Widget, such as `Documents\WileyWidget Imports`.
3. If you are exporting a list or an IIF file, back up the QuickBooks company file first.
4. Decide what you are exporting:
   - chart of accounts
   - customers or vendors
   - reports or transaction extracts

Current note:

- Wiley Widget currently imports chart of accounts, customers, vendors, and payment/check exports.
- QuickBooks item-list exports are not yet accepted because Wiley Widget does not currently have an item destination.

## File Types Wiley Widget Accepts

| File type | Best for                          | Recommendation                                                                    |
| --------- | --------------------------------- | --------------------------------------------------------------------------------- |
| CSV       | Lists and reports                 | Use this first when QuickBooks offers it                                          |
| IIF       | Advanced list/transaction exports | Use when QuickBooks only gives you IIF or when you need list/transaction fidelity |
| Excel     | Lists and reports                 | Use when CSV is not convenient                                                    |

## Export a CSV File from QuickBooks Desktop

Use the official Intuit article for the live menu wording and screenshots:
https://quickbooks.intuit.com/learn-support/en-us/help-article/manage-lists/import-export-csv-files/L9AiGRdT9_US_en_US

### Customers or Vendors

1. Open the Customer Center or Vendor Center.
2. Open the Excel dropdown.
3. Choose the export option for the list or transactions you need.
4. In the export window, choose the CSV option.
5. Save the file to your import folder.
6. Use a clear name, for example `QB_Customers_2026-03-18.csv`.

### Reports or Transactions

1. Open the report you want to export.
2. Use the Excel export command shown at the top of the report.
3. Choose to create a new worksheet in CSV format.
4. Save the file to your import folder.

## Export an IIF File from QuickBooks Desktop

Use the official Intuit articles for the live menu wording, warnings, and sample files:

- https://quickbooks.intuit.com/learn-support/en-us/help-article/import-export-data-files/export-import-edit-iif-files/L56LT9Z0Q_US_en_US
- https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/iif-overview-import-kit-sample-files-headers/L5CZIpJne_US_en_US

Steps:

1. Open QuickBooks Desktop.
2. Go to the File menu and use the Utilities or import/export area described in the Intuit article for your edition.
3. Choose the option to export an IIF file.
4. Save the `.iif` file to your import folder.
5. Do not open and resave the file in a program that changes tab-delimited formatting unless you know you need to edit it.

Important:

- Intuit describes IIF as an advanced format with limited validation.
- If you export IIF for transactions, keep the file exactly as exported unless instructed otherwise.
- If you are unsure, prefer CSV first.

## Export an Excel File from QuickBooks Desktop

Use the official Intuit article for live screenshots and current wording:
https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/import-export-ms-excel-files/L9BDPsTTX_US_en_US

Steps:

1. Open the list or report you want to export.
2. Use the Excel export command.
3. Save the workbook as `.xlsx` or `.xls` in your import folder.
4. Keep the header row so Wiley Widget can match the columns.

## Recommended File Naming

Use a name that tells you what the file contains and when you exported it.

Examples:

- `QB_ChartOfAccounts_2026-03-18.csv`
- `QB_Customers_2026-03-18.csv`
- `QB_Vendors_2026-03-18.xlsx`
- `QB_Transactions_Q1_2026.iif`

## Import Into Wiley Widget

1. Open Wiley Widget.
2. Go to the QuickBooks area.
3. Choose the import action that matches your export:
   - Import CSV/Excel for report exports in `.csv`, `.xlsx`, or `.xls`
   - Import Desktop File for the broader desktop import flow, including IIF files
4. Select the file you exported.
5. Wait while Wiley Widget auto-detects the export type and imports the records.
6. Review the completion message before closing the panel.

## Clerk Checklist

- The file is on the local machine.
- The file extension is `.csv`, `.iif`, `.xlsx`, or `.xls`.
- The file has a clear date in the name.
- The file opens correctly if you inspect it.
- You know whether the file represents accounts, customers, vendors, or a report export.

## Troubleshooting

### Wiley Widget says the file type is not supported

- Save the export again as CSV or Excel.
- If QuickBooks only produces IIF for that workflow, keep the `.iif` extension.

### Wiley Widget says the file does not match a supported QuickBooks Desktop export

- Confirm you exported accounts, customers, vendors, or a payment/report extract.
- If the file is an item-list export, that import is not yet supported.
- If you edited the file, make sure the header row still exists and the file still uses the original export columns.

### The file imports but the numbers look wrong

- For reports, confirm you exported the correct report and date range.
- For IIF files, re-export directly from QuickBooks and avoid spreadsheet reformatting.
- Keep the original export so you can compare it with the Wiley Widget preview.

## Official Help Links

- CSV export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/manage-lists/import-export-csv-files/L9AiGRdT9_US_en_US
- IIF export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/import-export-data-files/export-import-edit-iif-files/L56LT9Z0Q_US_en_US
- IIF format and sample files: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/iif-overview-import-kit-sample-files-headers/L5CZIpJne_US_en_US
- Excel export/import: https://quickbooks.intuit.com/learn-support/en-us/help-article/list-management/import-export-ms-excel-files/L9BDPsTTX_US_en_US
