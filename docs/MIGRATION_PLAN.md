# Migration Plan: Blazorise to Basic Blazor Components

## Motivation

- Reduce external dependencies
- Simplify maintenance
- Improve reliability
- Focus on core functionality over UI aesthetics

## Current Dependencies to Remove

- Blazorise
- Blazorise.Bootstrap5
- Blazorise.Icons.FontAwesome

## Migration Steps

### 1. Component Migration Priority

1. ✅ ScanControls.razor
2. ✅ MemoryValueHandler.razor
3. ✅ ScanExecutor.razor
4. ✅ MemoryScanner.razor (main page)

### 2. Basic Component Replacements

- ✅ Replace Blazorise.Button → button element with bootstrap classes
- ✅ Replace Blazorise.TextEdit → input elements
- ✅ Replace Blazorise.Select → select elements
- ✅ Replace Blazorise.DataGrid → basic HTML table
- ✅ Replace Blazorise.Alert → div with bootstrap alert classes
- ✅ Replace Blazorise.Validation → basic form validation
- ✅ Replace Blazorise.Card → div with card classes
- ✅ Replace Blazorise.Modal → div with modal classes
- ✅ Replace Blazorise.Icon → i with FontAwesome classes

### 3. CSS/Styling Strategy

- ✅ Use Bootstrap 5 classes directly
- ✅ Create minimal custom CSS for specific needs
- ✅ Maintain responsive design using Bootstrap grid

### 4. Implementation Order

1. ✅ Create base components library

   - ✅ Create basic input wrapper
   - ✅ Create basic button wrapper
   - ✅ Create basic table component
   - ✅ Create basic alert component

2. Component Migration

   - ✅ Migrate ScanControls.razor
   - ✅ Migrate MemoryValueHandler.razor
   - ✅ Migrate ScanExecutor.razor
   - ✅ Migrate MemoryScanner.razor

3. Testing
   - Update existing tests
   - Add new UI tests
   - Verify all functionality

### 5. Project Updates

1. Remove Blazorise packages from .csproj
2. Update Program.cs (remove Blazorise services)
3. Clean up \_Imports.razor
4. Update libman.json

## Testing Strategy

- Unit test each new base component
- Integration tests for complex interactions
- Visual regression testing
- Cross-browser testing

## Timeline Estimation

1. ✅ Base components creation: 1 day
2. ✅ Component migration: 2-3 days (Completed)
3. Testing and fixes: 1-2 days
4. Final cleanup: 1 day

## Success Criteria

- All functionality works as before
- No external UI component dependencies
- Improved performance
- Simplified codebase
- All tests passing
