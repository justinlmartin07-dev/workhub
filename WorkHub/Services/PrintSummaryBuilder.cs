using System.Net;
using System.Text;
using WorkHub.Models;

namespace WorkHub.Services;

// Renders job and customer summaries as print-ready HTML for handing to
// helpers who don't have the app. Standard business-document layout:
// customer name and address top-left, date top-right, contact details
// below the address, then parts and notes.
public static class PrintSummaryBuilder
{
    public static string BuildJobSummary(JobResponse job, CustomerResponse? customer)
    {
        var sb = new StringBuilder();
        var address = string.IsNullOrWhiteSpace(job.Address) ? customer?.Address : job.Address;
        AppendHeader(sb, "Job Summary", job.CustomerName, address);

        sb.Append("<div class='job-line'><span class='job-title'>").Append(E(job.Title))
          .Append("</span><span class='job-meta'>").Append(E(job.Status))
          .Append(" &middot; ").Append(E(job.Priority)).Append(" Priority</span></div>");

        var contactRows = new List<string>();
        if (job.MainContact is { } mc)
            contactRows.Add(ContactRow(mc.Name, mc.Role, mc.Phone, mc.Email, tag: "Main Contact"));
        if (customer != null)
        {
            foreach (var p in customer.Persons ?? [])
                if (p.Id != job.MainContact?.Id)
                    contactRows.Add(ContactRow(p.Name, p.Role, p.Phone, p.Email));
            if (customer.PrimaryPhone != null || customer.PrimaryEmail != null)
                contactRows.Add(ContactRow(customer.Name, "Office", customer.PrimaryPhone, customer.PrimaryEmail));
        }
        AppendContacts(sb, contactRows);

        AppendPartsTable(sb, "Parts Used", job.UsedItems);
        AppendPartsTable(sb, "Parts Needed", job.ToOrderItems);

        if (!string.IsNullOrWhiteSpace(job.ScopeNotes))
            sb.Append("<h2>Scope</h2><div class='prewrap'>").Append(E(job.ScopeNotes)).Append("</div>");

        if (job.Notes is { Count: > 0 } notes)
        {
            sb.Append("<h2>Notes</h2>");
            foreach (var n in notes)
                sb.Append("<div class='note'><div class='prewrap'>").Append(E(n.Content))
                  .Append("</div><div class='note-meta'>").Append(E(n.CreatedByName))
                  .Append(" &mdash; ").Append(n.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"))
                  .Append("</div></div>");
        }

        return CloseDocument(sb);
    }

    public static string BuildCustomerSummary(CustomerResponse customer)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, "Customer Summary", customer.Name, customer.Address);

        var contactRows = new List<string>();
        foreach (var p in customer.Persons ?? [])
            contactRows.Add(ContactRow(p.Name, p.Role, p.Phone, p.Email));
        foreach (var c in customer.Contacts ?? [])
            contactRows.Add(ContactRow(c.Label, null, c.Type == "phone" ? c.Value : null, c.Type == "email" ? c.Value : null));
        AppendContacts(sb, contactRows);

        if (!string.IsNullOrWhiteSpace(customer.Notes))
            sb.Append("<h2>Notes</h2><div class='prewrap'>").Append(E(customer.Notes)).Append("</div>");

        if (customer.Jobs is { Count: > 0 } jobs)
        {
            sb.Append("<h2>Jobs</h2><table><thead><tr><th>Job</th><th>Status</th><th>Priority</th></tr></thead><tbody>");
            foreach (var j in jobs)
                sb.Append("<tr><td>").Append(E(j.Title)).Append("</td><td>").Append(E(j.Status))
                  .Append("</td><td>").Append(E(j.Priority)).Append("</td></tr>");
            sb.Append("</tbody></table>");
        }

        return CloseDocument(sb);
    }

    private static void AppendHeader(StringBuilder sb, string docType, string name, string? address)
    {
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>")
          .Append("<meta name='viewport' content='width=device-width, initial-scale=1'>")
          .Append("<title>").Append(E(docType)).Append("</title><style>").Append(Css)
          .Append("</style></head><body><div class='sheet'>")
          .Append("<div class='doc-header'><div><div class='customer-name'>").Append(E(name)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(address))
            sb.Append("<div class='address'>").Append(MultiLine(address)).Append("</div>");
        sb.Append("</div><div class='doc-info'><div class='doc-type'>").Append(E(docType).ToUpperInvariant())
          .Append("</div><div class='doc-date'>").Append(DateTime.Now.ToString("MMMM d, yyyy"))
          .Append("</div></div></div><hr class='rule'>");
    }

    private static string CloseDocument(StringBuilder sb)
        => sb.Append("</div></body></html>").ToString();

    private static void AppendContacts(StringBuilder sb, List<string> rows)
    {
        if (rows.Count == 0) return;
        sb.Append("<h2>Contacts</h2>");
        foreach (var row in rows) sb.Append(row);
    }

    private static string ContactRow(string name, string? role, string? phone, string? email, string? tag = null)
    {
        var sb = new StringBuilder("<div class='contact'><span class='contact-name'>").Append(E(name));
        if (!string.IsNullOrWhiteSpace(role)) sb.Append(" &mdash; ").Append(E(role));
        if (tag != null) sb.Append(" <span class='tag'>").Append(E(tag)).Append("</span>");
        sb.Append("</span>");
        if (!string.IsNullOrWhiteSpace(phone)) sb.Append("<span class='contact-detail'>").Append(E(phone)).Append("</span>");
        if (!string.IsNullOrWhiteSpace(email)) sb.Append("<span class='contact-detail'>").Append(E(email)).Append("</span>");
        return sb.Append("</div>").ToString();
    }

    private static void AppendPartsTable(StringBuilder sb, string title, List<JobItemResponse>? items)
    {
        if (items == null || items.Count == 0) return;
        sb.Append("<h2>").Append(E(title)).Append("</h2>")
          .Append("<table><thead><tr><th>Item</th><th>Part #</th><th class='qty'>Qty</th></tr></thead><tbody>");
        foreach (var i in items)
        {
            sb.Append("<tr><td>").Append(E(i.Name));
            if (!string.IsNullOrWhiteSpace(i.Description))
                sb.Append("<div class='item-desc'>").Append(E(i.Description)).Append("</div>");
            sb.Append("</td><td>").Append(E(i.PartNumber)).Append("</td><td class='qty'>")
              .Append(i.Quantity).Append("</td></tr>");
        }
        sb.Append("</tbody></table>");
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private static string MultiLine(string s)
        => string.Join("<br>", s.Split('\n').Select(l => E(l.Trim())));

    // Screen shows a paper sheet on a gray desk; @media print strips the chrome
    // and lets @page margins take over, so the printout is a plain document.
    private const string Css = """
        @page { margin: 0.6in; }
        * { box-sizing: border-box; }
        body { margin:0; background:#cbd5e1; font-family:'Segoe UI',Roboto,Arial,sans-serif; font-size:13px; color:#111; -webkit-print-color-adjust:exact; }
        .sheet { background:#fff; width:100%; max-width:8.5in; margin:16px auto; padding:0.6in; box-shadow:0 2px 10px rgba(0,0,0,0.35); min-height:10in; }
        .doc-header { display:flex; justify-content:space-between; align-items:flex-start; gap:24px; }
        .customer-name { font-size:20px; font-weight:700; }
        .address { margin-top:6px; line-height:1.45; }
        .doc-info { text-align:right; flex-shrink:0; }
        .doc-type { font-size:14px; font-weight:700; letter-spacing:2px; color:#334155; }
        .doc-date { margin-top:4px; }
        .rule { border:none; border-top:2px solid #111; margin:14px 0 6px; }
        h2 { font-size:12px; letter-spacing:1px; text-transform:uppercase; border-bottom:1px solid #94a3b8; padding-bottom:3px; margin:18px 0 8px; }
        .job-line { display:flex; justify-content:space-between; align-items:baseline; gap:16px; margin-top:10px; }
        .job-title { font-size:16px; font-weight:700; }
        .job-meta { color:#334155; white-space:nowrap; }
        .contact { display:flex; flex-wrap:wrap; gap:4px 18px; padding:3px 0; }
        .contact-name { font-weight:600; min-width:2in; }
        .tag { font-weight:400; font-size:11px; color:#334155; border:1px solid #94a3b8; border-radius:3px; padding:0 4px; }
        table { width:100%; border-collapse:collapse; }
        th { text-align:left; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; color:#334155; border-bottom:1px solid #111; padding:4px 8px 4px 0; }
        td { border-bottom:1px solid #e2e8f0; padding:5px 8px 5px 0; vertical-align:top; }
        .qty { text-align:right; width:3em; }
        .item-desc { color:#475569; font-size:12px; }
        .prewrap { white-space:pre-wrap; line-height:1.45; }
        .note { margin-bottom:10px; }
        .note-meta { color:#475569; font-size:11px; margin-top:2px; }
        @media screen and (max-width:600px) { .sheet { padding:20px; margin:8px; min-height:0; } }
        @media print { body { background:#fff; } .sheet { box-shadow:none; margin:0; padding:0; max-width:none; min-height:0; } }
        """;
}
