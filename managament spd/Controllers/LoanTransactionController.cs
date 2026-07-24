using ClosedXML.Excel;
using ManagementSPD.Data;
using ManagementSPD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ManagementSPD.Services;

namespace ManagementSPD.Controllers
{
    [Authorize]
    public class LoanTransactionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public LoanTransactionController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private async Task<List<LoanTransaction>> GetFilteredTransactions(User currentUser, string search, string status, string empId, DateTime? start, DateTime? end)
        {
            var query = _context.LoanTransactions
              .Include(t => t.License)
              .Include(t => t.Employee)
              .Include(t => t.Staff)
              .AsQueryable();

            if (currentUser.Role == "Employee")
            {
                query = query.Where(t => t.EmployeeID == currentUser.Id);
            }
            else if (!string.IsNullOrEmpty(empId) && int.TryParse(empId, out int eId))
            {
                query = query.Where(t => t.EmployeeID == eId);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(t => t.License.LicenseName.Contains(search));
            }

            if (start.HasValue) query = query.Where(t => t.RequestDate.Date >= start.Value.Date);
            if (end.HasValue) query = query.Where(t => t.RequestDate.Date <= end.Value.Date);

            var data = await query.OrderByDescending(t => t.RequestDate).ToListAsync();

            if (!string.IsNullOrEmpty(status))
            {
                var today = DateTime.Today;
                if (status == "Active") data = data.Where(t => (t.Status == "Approved" || t.Status == "Pending") && t.DueDate > today.AddDays(7)).ToList();
                else if (status == "Expired Soon") data = data.Where(t => (t.Status == "Approved" || t.Status == "Pending") && t.DueDate >= today && t.DueDate <= today.AddDays(7)).ToList();
                else if (status == "Expired") data = data.Where(t => (t.Status == "Approved" || t.Status == "Pending") && t.DueDate < today).ToList();
                else if (status == "Returned") data = data.Where(t => t.Status == "Returned").ToList();
            }
            return data;
        }

        private string GenerateFileName(string baseName, string search, string status, DateTime? start, DateTime? end, string extension)
        {
            string fileName = baseName;
            if (!string.IsNullOrEmpty(status)) fileName += $"_{status}";
            if (!string.IsNullOrEmpty(search)) fileName += $"_{search}";
            if (start.HasValue) fileName += $"_From{start.Value:yyyyMMdd}";
            if (end.HasValue) fileName += $"_To{end.Value:yyyyMMdd}";
            fileName += $"_{DateTime.Now:HHmm}";
            return fileName.Replace(" ", "_") + extension;
        }

        public async Task<IActionResult> Index(string searchLicense, string statusFilter, string employeeFilter, DateTime? startDate, DateTime? endDate)
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            ViewBag.SearchLicense = searchLicense;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.EmployeeFilter = employeeFilter;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            if (currentUser.Role == "Staff" || currentUser.Role == "MasterAdmin")
            {
                var employees = await _context.Users.Where(u => u.Role == "Employee")
                  .Select(u => new { u.Id, FullName = u.Username + " (" + u.EmployeeNo + ")" }).ToListAsync();
                ViewBag.EmployeeList = new SelectList(employees, "Id", "FullName", employeeFilter);
            }

            var transactions = await GetFilteredTransactions(currentUser, searchLicense, statusFilter, employeeFilter, startDate, endDate);
            return View(transactions);
        }

        [Authorize(Roles = "Staff, MasterAdmin")]
        public async Task<IActionResult> ExportExcel(string searchLicense, string statusFilter, string employeeFilter, DateTime? startDate, DateTime? endDate)
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            var data = await GetFilteredTransactions(currentUser, searchLicense, statusFilter, employeeFilter, startDate, endDate);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Licenses Report");
                worksheet.ShowGridLines = false;
                int lastCol = 8;

                string reportTitle = string.IsNullOrEmpty(statusFilter) ? "LICENSES MANAGEMENT REPORT" : $"LOAN REPORT - {statusFilter.ToUpper()}";

                var titleRange = worksheet.Range(1, 1, 1, lastCol);
                titleRange.Merge().Value = reportTitle;
                titleRange.Style.Font.Bold = true;
                titleRange.Style.Font.FontSize = 18;
                titleRange.Style.Font.FontColor = XLColor.White;
                titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2c3e50");
                titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                worksheet.Row(1).Height = 35;
                worksheet.Cell("A2").Value = "Generated Date:";
                worksheet.Cell("B2").Value = DateTime.Now.ToString("dd MMMM yyyy, HH:mm");
                worksheet.Cell("A3").Value = "Downloaded By:";
                worksheet.Cell("B3").Value = $"{currentUser.Username} ({currentUser.EmployeeNo})";

                var infoRange = worksheet.Range("A2:B3");
                infoRange.Style.Font.FontSize = 10;
                infoRange.Style.Font.Italic = true;
                infoRange.Style.Font.FontColor = XLColor.DarkGray;

                int headerRow = 5;
                var headers = new[] { "Item Name", "Employee No", "Qty", "Request Date", "Due Date", "Return Date", "Status", "Remarks" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(headerRow, i + 1).Value = headers[i];
                }

                var headerRange = worksheet.Range(headerRow, 1, headerRow, lastCol);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4e73df");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                worksheet.Row(headerRow).Height = 25;

                int row = 6;
                foreach (var item in data)
                {
                    worksheet.Cell(row, 1).Value = item.License?.LicenseName;
                    worksheet.Cell(row, 2).Value = item.Employee?.EmployeeNo;
                    worksheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    worksheet.Cell(row, 3).Value = item.Qty;
                    worksheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    worksheet.Cell(row, 4).Value = item.RequestDate;
                    worksheet.Cell(row, 5).Value = item.DueDate;

                    string returnDateStr = item.ReturnDate.HasValue ? item.ReturnDate.Value.ToString("dd-MMM-yyyy") : "-";
                    worksheet.Cell(row, 6).Value = returnDateStr;
                    worksheet.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    worksheet.Cell(row, 8).Value = item.Remarks;

                    var statusCell = worksheet.Cell(row, 7);
                    statusCell.Value = item.Status;
                    statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    statusCell.Style.Font.Bold = true;

                    string statusText = item.Status?.ToLower() ?? "";
                    if (statusText == "approved") { statusCell.Style.Font.FontColor = XLColor.DarkGreen; statusCell.Style.Fill.BackgroundColor = XLColor.LightGreen; }
                    else if (statusText == "rejected") { statusCell.Style.Font.FontColor = XLColor.DarkRed; statusCell.Style.Fill.BackgroundColor = XLColor.LightPink; }
                    else if (statusText == "pending") { statusCell.Style.Font.FontColor = XLColor.DarkOrange; statusCell.Style.Fill.BackgroundColor = XLColor.LightYellow; }
                    else if (statusText == "returned") { statusCell.Style.Font.FontColor = XLColor.White; statusCell.Style.Fill.BackgroundColor = XLColor.Gray; }

                    worksheet.Range(row, 1, row, lastCol).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    worksheet.Range(row, 1, row, lastCol).Style.Border.BottomBorderColor = XLColor.LightGray;
                    row++;
                }

                worksheet.Column(4).Style.DateFormat.Format = "dd-MMM-yyyy";
                worksheet.Column(5).Style.DateFormat.Format = "dd-MMM-yyyy";
                worksheet.Columns().AdjustToContents();

                if (row > 6)
                {
                    var tableRange = worksheet.Range(headerRow, 1, row - 1, lastCol);
                    tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                    tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#4e73df");
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    string fileName = GenerateFileName("LicensesReport_Pro", searchLicense, statusFilter, startDate, endDate, ".xlsx");
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        [Authorize(Roles = "Staff, MasterAdmin")]
        public async Task<IActionResult> ExportPdf(string searchLicense, string statusFilter, string employeeFilter, DateTime? startDate, DateTime? endDate)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            var data = await GetFilteredTransactions(currentUser, searchLicense, statusFilter, employeeFilter, startDate, endDate);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Text(text => {
                        text.Span("Licenses Management Report").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        text.EmptyLine();
                        text.Span($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        text.EmptyLine();
                        text.Span($"Downloaded by: {currentUser.Username} ({currentUser.EmployeeNo})").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns => {
                            columns.RelativeColumn(); columns.ConstantColumn(80); columns.ConstantColumn(30);
                            columns.ConstantColumn(70); columns.ConstantColumn(70); columns.ConstantColumn(70);
                        });
                        table.Header(header => {
                            static IContainer HeaderStyle(IContainer container) => container.Background(Colors.Blue.Darken2).Padding(5).DefaultTextStyle(x => x.FontColor(Colors.White).Bold());
                            header.Cell().Element(HeaderStyle).Text("Item Name");
                            header.Cell().Element(HeaderStyle).Text("Emp. No");
                            header.Cell().Element(HeaderStyle).Text("Qty");
                            header.Cell().Element(HeaderStyle).Text("Req. Date");
                            header.Cell().Element(HeaderStyle).Text("Due Date");
                            header.Cell().Element(HeaderStyle).Text("Status");
                        });
                        foreach (var item in data)
                        {
                            static IContainer BodyStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                            table.Cell().Element(BodyStyle).Text(item.License?.LicenseName ?? "-");
                            table.Cell().Element(BodyStyle).Text(item.Employee?.EmployeeNo ?? "-");
                            table.Cell().Element(BodyStyle).AlignCenter().Text(item.Qty.ToString());
                            table.Cell().Element(BodyStyle).Text(item.RequestDate.ToString("dd-MMM-yy"));
                            table.Cell().Element(BodyStyle).Text(item.DueDate.ToString("dd-MMM-yy"));
                            string statusColor = item.Status == "Approved" ? Colors.Green.Medium : item.Status == "Rejected" ? Colors.Red.Medium : item.Status == "Returned" ? Colors.Blue.Medium : Colors.Orange.Medium;
                            table.Cell().Element(BodyStyle).Text(t => { t.Span(item.Status).FontColor(statusColor).Bold(); });
                        }
                    });
                    page.Footer().AlignCenter().Text(x => { x.Span("Page "); x.CurrentPageNumber(); });
                });
            });

            using (var stream = new MemoryStream())
            {
                pdf.GeneratePdf(stream);
                string fileName = GenerateFileName("LicensesReport", searchLicense, statusFilter, startDate, endDate, ".pdf");
                return File(stream.ToArray(), "application/pdf", fileName);
            }
        } 

        [HttpGet]
        public async Task<IActionResult> Create(string licenseName)
        {
            if (!string.IsNullOrEmpty(licenseName))
            {
                var existingItem = await _context.Licenses.FirstOrDefaultAsync(l => l.LicenseName.ToLower() == licenseName.ToLower());

                if (existingItem != null)
                {
                    ViewBag.AvailableStock = existingItem.AvailableQuantity; //add validation stock in column quantity form request
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoanTransaction model, string LicenseName)
        {
            var username = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (!string.IsNullOrEmpty(LicenseName))
            {
                var existingItem = await _context.Licenses.FirstOrDefaultAsync(i => i.LicenseName.ToLower() == LicenseName.ToLower());

                if (existingItem != null)
                {
                    if (existingItem.AvailableQuantity < model.Qty)
                    {
                        TempData["ErrorMessage"] = $"Stock is not enough. Available: {existingItem.AvailableQuantity}.";
                        return RedirectToAction(nameof(Index));
                    }
                    model.LicenseID = existingItem.LicenseID;
                }
                else
                {
                    var newItem = new License
                    {
                        LicenseName = LicenseName,
                        Category = "Software",
                        Description = "Auto-created from loan request",
                        TotalQuantity = model.Qty, 
                        AvailableQuantity = model.Qty 
                    };
                    _context.Licenses.Add(newItem);
                    await _context.SaveChangesAsync();

                    model.LicenseID = newItem.LicenseID;
                }
            }

            ModelState.Remove("LicenseID"); ModelState.Remove("License"); ModelState.Remove("Employee");
            ModelState.Remove("Staff"); ModelState.Remove("Status"); ModelState.Remove("EmployeeID"); ModelState.Remove("LoanApprovals");

            if (ModelState.IsValid)
            {
                model.EmployeeID = currentUser.Id;
                model.Status = "Pending";
                if (model.RequestDate == default) model.RequestDate = DateTime.Now;
                if (model.DueDate == default) model.DueDate = DateTime.Now.AddDays(7);

                _context.LoanTransactions.Add(model);
                await _context.SaveChangesAsync();

                var staffUsers = await _context.Users
                    .Where(u => u.Role == "Staff" || u.Role == "MasterAdmin")
                    .ToListAsync();

                foreach (var staff in staffUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserID = staff.Id,
                        Message = $"New Request: {LicenseName} by {currentUser.Username}",
                        CreatedAt = DateTime.Now,
                        TransactionID = model.TransactionID
                    });

                    if (!string.IsNullOrEmpty(staff.Email))
                    {
                        string emailBody = $@"
                            <h3>New Loan Request</h3>
                            <p>User <b>{currentUser.Username} ({currentUser.EmployeeNo})</b> has requested a loan:</p>
                            <ul>
                                <li>Item: {LicenseName}</li>
                                <li>Quantity: {model.Qty}</li>
                                <li>Due Date: {model.DueDate:dd MMM yyyy}</li>
                            </ul>
                            <p>Please log in to the system to process this request.</p>";

                        await _emailService.SendEmailAsync(staff.Email, $"[NEW REQUEST] {LicenseName} - {currentUser.Username}", emailBody);
                    }
                }

                var audit = new AuditLog
                {
                    UserID = currentUser.Id,
                    Action = "Create",
                    TableName = "LoanTransactions",
                    RecordID = model.TransactionID.ToString(),
                    Details = $"Created loan request for: {LicenseName}",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(audit);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Request submitted successfully!";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to submit request. Please check your inputs.";
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Staff, MasterAdmin")]
        [Route("LoanTransaction/Edit/{id}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest("ID is missing");

            var transaction = await _context.LoanTransactions
                .Include(t => t.License)
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.TransactionID == id);

            if (transaction == null) return NotFound("Transaction not found");

            return PartialView("_EditLoan", transaction);
        }

        [HttpPost]
        [Authorize(Roles = "Staff, MasterAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int TransactionID, LoanTransaction model)
        {
            if (TransactionID != model.TransactionID) return NotFound();

            var original = await _context.LoanTransactions
                .Include(t => t.License)
                .FirstOrDefaultAsync(t => t.TransactionID == TransactionID);

            if (original == null) return NotFound();

            ModelState.Remove("License");
            ModelState.Remove("Employee");
            ModelState.Remove("Staff");
            ModelState.Remove("LoanApprovals");

            if (ModelState.IsValid)
            {
                try
                {
                    string oldStatus = original.Status;

                    original.Qty = model.Qty;
                    original.DueDate = model.DueDate;
                    original.Status = model.Status;
                    original.Remarks = model.Remarks;

                    var currentUserName = User.Identity.Name;
                    var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUserName);

                    if (currentUser != null)
                    {
                        var audit = new AuditLog
                        {
                            UserID = currentUser.Id,
                            Action = "Update Transaction",
                            TableName = "LoanTransactions",
                            RecordID = TransactionID.ToString(),
                            Details = $"Edited loan item '{original.License?.LicenseName}'. Status: {oldStatus}->{model.Status}.",
                            Timestamp = DateTime.Now
                        };
                        _context.AuditLogs.Add(audit);
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Transaction updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.LoanTransactions.Any(e => e.TransactionID == TransactionID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Failed to update transaction.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Staff, MasterAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRequest(int id, string actionType, string remarks)
        {
            var transaction = await _context.LoanTransactions.Include(t => t.License).FirstOrDefaultAsync(t => t.TransactionID == id);
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (transaction == null || currentUser == null) return NotFound();

            if (actionType == "Approve")
            {
                if (transaction.License != null)
                {
                    if (transaction.License.AvailableQuantity < transaction.Qty)
                    {
                        TempData["ErrorMessage"] = "Insufficient stock to approve this request.";
                        return RedirectToAction(nameof(Index));
                    }
                    transaction.License.AvailableQuantity -= transaction.Qty;
                }
                transaction.Status = "Approved";
                transaction.ApprovalDate = DateTime.Now;
            }
            else if (actionType == "Reject") { transaction.Status = "Rejected"; }

            transaction.StaffID = currentUser.Id;
            var log = new LoanApproval { TransactionID = transaction.TransactionID, StaffID = currentUser.Id, ApprovalStatus = transaction.Status, Remarks = remarks, CreatedAt = DateTime.Now };
            _context.LoanApprovals.Add(log);

            if (transaction.EmployeeID.HasValue)
            {
                var employee = await _context.Users.FindAsync(transaction.EmployeeID.Value);
                _context.Notifications.Add(new Notification { UserID = transaction.EmployeeID.Value, Message = $"Your request has been {transaction.Status}.", CreatedAt = DateTime.Now, TransactionID = transaction.TransactionID });

                if (employee != null && !string.IsNullOrEmpty(employee.Email))
                {
                    string emailBody = $@"
                        <h3>Request {transaction.Status}</h3>
                        <p>Dear {employee.Username},</p>
                        <p>Your request for <b>{transaction.License?.LicenseName}</b> has been <b>{transaction.Status}</b>.</p>
                        <p>Remarks: {remarks}</p>
                        <br/>
                        <p>Best Regards,<br/>Management SPD System</p>";

                    await _emailService.SendEmailAsync(employee.Email, $"[UPDATE] Request {transaction.Status}: {transaction.License?.LicenseName}", emailBody);
                }
            }

            var audit = new AuditLog
            {
                UserID = currentUser.Id,
                Action = actionType,
                TableName = "LoanTransactions",
                RecordID = transaction.TransactionID.ToString(),
                Details = $"{actionType} request for item '{transaction.License?.LicenseName}'",
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(audit);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Transaction has been {transaction.Status}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnItem(int id)
        {
            var transaction = await _context.LoanTransactions.Include(t => t.License).FirstOrDefaultAsync(t => t.TransactionID == id);
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (transaction == null || currentUser == null || transaction.EmployeeID != currentUser.Id) return NotFound();

            if (transaction.Status == "Approved")
            {
                transaction.Status = "Returned";
                transaction.ReturnDate = DateTime.Now;

                // Return Stock
                if (transaction.License != null)
                {
                    transaction.License.AvailableQuantity += transaction.Qty;
                }

                var log = new LoanApproval { TransactionID = transaction.TransactionID, StaffID = currentUser.Id, ApprovalStatus = "Returned", Remarks = "Returned by employee", CreatedAt = DateTime.Now };
                _context.LoanApprovals.Add(log);

                var recipientUsers = await _context.Users.Where(u => u.Role == "Staff" || u.Role == "MasterAdmin").ToListAsync();
                foreach (var recipient in recipientUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserID = recipient.Id,
                        Message = $"Item '{transaction.License.LicenseName}' RETURNED by {currentUser.EmployeeNo}.",
                        CreatedAt = DateTime.Now,
                        TransactionID = transaction.TransactionID
                    });

                    if (!string.IsNullOrEmpty(recipient.Email))
                    {
                        string emailBody = $@"
                            <h3>Item Returned</h3>
                            <p>User <b>{currentUser.Username} ({currentUser.EmployeeNo})</b> has returned an item:</p>
                            <ul>
                                <li>Item: {transaction.License?.LicenseName}</li>
                                <li>Return Date: {DateTime.Now:dd MMM yyyy HH:mm}</li>
                            </ul>";

                        await _emailService.SendEmailAsync(recipient.Email, $"[RETURNED] {transaction.License?.LicenseName} - {currentUser.Username}", emailBody);
                    }
                }

                var audit = new AuditLog
                {
                    UserID = currentUser.Id,
                    Action = "Return",
                    TableName = "LoanTransactions",
                    RecordID = transaction.TransactionID.ToString(),
                    Details = $"Returned item '{transaction.License.LicenseName}'",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(audit);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Item returned successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Only approved items can be returned.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Staff, MasterAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var transaction = await _context.LoanTransactions
                .Include(t => t.License)
                .Include(t => t.LoanApprovals)
                .FirstOrDefaultAsync(t => t.TransactionID == id);

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            if (transaction != null)
            {
                try
                {
                    if (transaction.LoanApprovals != null && transaction.LoanApprovals.Any())
                    {
                        _context.LoanApprovals.RemoveRange(transaction.LoanApprovals);
                    }

                    var relatedNotifs = await _context.Notifications.Where(n => n.TransactionID == id).ToListAsync();
                    if (relatedNotifs.Any())
                    {
                        _context.Notifications.RemoveRange(relatedNotifs);
                    }

                    _context.LoanTransactions.Remove(transaction);

                    if (currentUser != null)
                    {
                        var audit = new AuditLog
                        {
                            UserID = currentUser.Id,
                            Action = "Delete",
                            TableName = "LoanTransactions",
                            RecordID = id.ToString(),
                            Details = $"Deleted transaction for item '{transaction.License?.LicenseName ?? "Unknown"}'",
                            Timestamp = DateTime.Now
                        };
                        _context.AuditLogs.Add(audit);
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Transaction deleted successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to delete: " + ex.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Transaction not found.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            var transaction = await _context.LoanTransactions.Include(t => t.License).Include(t => t.Employee).Include(t => t.Staff).FirstOrDefaultAsync(t => t.TransactionID == id);
            if (transaction == null) return NotFound();

            return Json(new
            {
                id = transaction.TransactionID,
                itemName = transaction.License?.LicenseName ?? "Unknown",
                category = transaction.License?.Category ?? "-",
                employeeName = transaction.Employee?.Username ?? "-",
                employeeNo = transaction.Employee?.EmployeeNo ?? "-",
                qty = transaction.Qty,
                requestDate = transaction.RequestDate.ToString("dd MMM yyyy"),
                dueDate = transaction.DueDate.ToString("dd MMM yyyy"),
                returnDate = transaction.ReturnDate.HasValue ? transaction.ReturnDate.Value.ToString("dd MMM yyyy") : "-",
                status = transaction.Status,
                remarks = transaction.Remarks ?? "-",
                processedBy = transaction.Staff?.Username ?? "-"
            });
        }
    }
}