# UtilityCustomerView

## Overview

The UtilityCustomerView provides a comprehensive interface for managing municipal utility customers. It supports full CRUD operations (Create, Read, Update, Delete) for customer records, billing history management, and customer search functionality.

## Purpose

This view serves as the primary customer management interface for utility billing operations, allowing users to:

- View and search customer records
- Add new customers
- Edit existing customer information
- Delete customer records
- View billing history for selected customers
- Process bill payments

## Architecture

### MVVM Pattern

- **View**: `UtilityCustomerView.xaml` - WPF Window with Syncfusion controls
- **ViewModel**: `UtilityCustomerViewModel.cs` - Implements INotifyDataErrorInfo for validation
- **Model**: `UtilityCustomer.cs` - Entity model with data annotations

### Key Components

- **Customer Grid**: SfDataGrid displaying customer list with sorting/filtering
- **Bill History Grid**: SfDataGrid showing billing records for selected customer
- **Customer Details Form**: Data entry form with validation
- **Ribbon Toolbar**: Command buttons for operations

## Data Contract

### Input Data

- Customer repository (IUtilityCustomerRepository)
- Grok AI service (IGrokSupercomputer) for customer analysis

### Output Data

- ObservableCollection<UtilityCustomer> - Customer list
- ObservableCollection<UtilityBill> - Bills for selected customer
- Validation errors via INotifyDataErrorInfo

## Key Features

### Customer Management

- Load all customers or filter by status (active, outside city limits)
- Search customers by name, account number, or address
- Add new customers with auto-generated account numbers
- Edit customer details with real-time validation
- Delete customers (with referential integrity checks)

### Billing Integration

- View billing history for selected customers
- Display bill status (paid, overdue, sent)
- Process bill payments
- Export billing data

### Validation

- Real-time field validation using INotifyDataErrorInfo
- Required field validation
- Format validation (phone, email, ZIP code)
- Business rule validation

### Accessibility

- Full keyboard navigation support
- AutomationProperties for screen readers
- Logical tab order
- Focus management on load

## Dependencies

- **Prism.Wpf**: MVVM framework and region management
- **Syncfusion.SfSkinManager.WPF**: Theming and UI controls
- **Syncfusion.SfGrid.WPF**: Data grid controls
- **Serilog**: Logging framework
- **Entity Framework Core**: Data access

## Configuration

### Theme Support

- Uses dynamic resources from WileyTheme.xaml
- Supports FluentDark theme with acrylic effects
- Theme switching handled by ThemeManager

### Keyboard Shortcuts

- F5: Load all customers
- Ctrl+N: Add new customer
- Ctrl+S: Save customer changes
- Delete: Delete selected customer
- Ctrl+F: Search customers
- Escape: Clear search

## Constraints and Limitations

- Customer deletion prevented if referenced by billing records
- Account numbers auto-generated and cannot be manually edited
- Some operations require database connectivity
- Large datasets may impact performance (virtualization enabled)

## Error Handling

- Database connection errors displayed to user
- Validation errors shown inline with visual cues
- Operation cancellation support for long-running tasks
- Comprehensive logging for troubleshooting

## Performance Considerations

- SfDataGrid virtualization enabled for large datasets
- Async operations prevent UI freezing
- Cancellation tokens for operation cancellation
- Efficient data binding with ObservableCollection

## Testing

### Unit Tests Required

- ViewModel command execution
- Validation logic
- Error handling scenarios
- Property change notifications

### Integration Tests Required

- Database operations
- Navigation and region injection
- Theme switching behavior

## Future Enhancements

- Customer import/export functionality
- Advanced search filters
- Customer merge/split operations
- Bulk operations support
- Customer communication history</content>
  <parameter name="filePath">c:\Users\biges\Desktop\Wiley_Widget\docs\views\UtilityCustomerView.md
