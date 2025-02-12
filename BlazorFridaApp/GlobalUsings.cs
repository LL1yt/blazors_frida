// System
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Linq.Expressions;
global using System.Diagnostics;
global using System.Runtime.InteropServices;

// ASP.NET Core and Blazor
global using Microsoft.AspNetCore.Components;
global using Microsoft.AspNetCore.Components.Web;
global using Microsoft.AspNetCore.Components.Routing;
global using Microsoft.AspNetCore.Components.Forms;

// Memory Scanner Core
global using BlazorFridaApp.MemoryScanner;
global using BlazorFridaApp.MemoryScanner.Models;
global using BlazorFridaApp.MemoryScanner.Services;
global using BlazorFridaApp.MemoryScanner.Services.Interfaces;
global using BlazorFridaApp.MemoryScanner.Components;
global using static BlazorFridaApp.MemoryScanner.Models.ScanType;
global using static BlazorFridaApp.MemoryScanner.Models.MemoryValueType;

// Database and Persistence
global using BlazorFridaApp.Persistence;
global using Microsoft.EntityFrameworkCore;

// Services
global using BlazorFridaApp.Services;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// UI Components
global using BlazorFridaApp.Components;
global using Blazorise;
global using Blazorise.Bootstrap5;