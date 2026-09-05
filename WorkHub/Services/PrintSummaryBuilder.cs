using System.Text;
using WorkHub.Models;

namespace WorkHub.Services;

// Builds the data models for the printable summaries and renders them through
// the HTML templates (fetched from the API by PrintTemplateService, so layout
// changes deploy server-side without a client update). The template token
// reference lives in the templates themselves: WorkHub.Api/Templates/*.html.
// The BuildXxxText methods render the same data as compact plain text for
// sharing into SMS/messaging apps.
public static class PrintSummaryBuilder
{
    public static string BuildJobSummary(string template, JobResponse job, CustomerResponse? customer)
    {
        var contacts = CollectJobContacts(job, customer);

        var model = new Dictionary<string, object?>
        {
            ["customer_name"] = job.CustomerName,
            ["address"] = string.IsNullOrWhiteSpace(job.Address) ? customer?.Address : job.Address,
            ["date"] = DateTime.Now.ToString("MMMM d, yyyy"),
            ["job_title"] = job.Title,
            ["status"] = job.Status,
            ["priority"] = job.Priority,
            ["contacts"] = contacts,
            ["has_contacts"] = contacts.Count > 0,
            ["parts_used"] = Parts(job.UsedItems),
            ["has_parts_used"] = job.UsedItems is { Count: > 0 },
            ["parts_needed"] = Parts(job.ToOrderItems),
            ["has_parts_needed"] = job.ToOrderItems is { Count: > 0 },
            ["scope"] = job.ScopeNotes,
            ["notes"] = (job.Notes ?? []).Select(n => new Dictionary<string, object?>
            {
                ["content"] = n.Content,
                ["author"] = n.CreatedByName,
                ["date"] = n.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
            }).ToList(),
            ["has_notes"] = job.Notes is { Count: > 0 },
        };

        return TemplateRenderer.Render(template, model);
    }

    public static string BuildCustomerSummary(string template, CustomerResponse customer)
    {
        var contacts = CollectCustomerContacts(customer);

        var model = new Dictionary<string, object?>
        {
            ["customer_name"] = customer.Name,
            ["address"] = customer.Address,
            ["date"] = DateTime.Now.ToString("MMMM d, yyyy"),
            ["contacts"] = contacts,
            ["has_contacts"] = contacts.Count > 0,
            ["notes"] = customer.Notes,
            ["jobs"] = (customer.Jobs ?? []).Select(j => new Dictionary<string, object?>
            {
                ["title"] = j.Title,
                ["status"] = j.Status,
                ["priority"] = j.Priority,
            }).ToList(),
            ["has_jobs"] = customer.Jobs is { Count: > 0 },
        };

        return TemplateRenderer.Render(template, model);
    }

    public static string BuildJobSummaryText(JobResponse job, CustomerResponse? customer)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"JOB SUMMARY — {DateTime.Now:MMMM d, yyyy}");
        sb.AppendLine();
        sb.AppendLine(job.CustomerName);
        var address = string.IsNullOrWhiteSpace(job.Address) ? customer?.Address : job.Address;
        if (!string.IsNullOrWhiteSpace(address))
            sb.AppendLine(address.Trim());
        sb.AppendLine();
        sb.AppendLine(job.Title);
        sb.AppendLine($"{job.Status} · {job.Priority} Priority");

        AppendContacts(sb, CollectJobContacts(job, customer));
        AppendParts(sb, "PARTS USED", job.UsedItems);
        AppendParts(sb, "PARTS NEEDED", job.ToOrderItems);

        if (!string.IsNullOrWhiteSpace(job.ScopeNotes))
        {
            sb.AppendLine();
            sb.AppendLine("SCOPE");
            sb.AppendLine(job.ScopeNotes.Trim());
        }

        if (job.Notes is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("NOTES");
            var first = true;
            foreach (var n in job.Notes)
            {
                if (!first) sb.AppendLine();
                first = false;
                sb.AppendLine(n.Content.Trim());
                sb.AppendLine($"— {n.CreatedByName}, {n.CreatedAt.ToLocalTime():MMM d, yyyy}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildCustomerSummaryText(CustomerResponse customer)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CUSTOMER SUMMARY — {DateTime.Now:MMMM d, yyyy}");
        sb.AppendLine();
        sb.AppendLine(customer.Name);
        if (!string.IsNullOrWhiteSpace(customer.Address))
            sb.AppendLine(customer.Address.Trim());

        AppendContacts(sb, CollectCustomerContacts(customer));

        if (!string.IsNullOrWhiteSpace(customer.Notes))
        {
            sb.AppendLine();
            sb.AppendLine("NOTES");
            sb.AppendLine(customer.Notes.Trim());
        }

        if (customer.Jobs is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("JOBS");
            foreach (var j in customer.Jobs)
                sb.AppendLine($"{j.Title} — {j.Status} · {j.Priority} Priority");
        }

        return sb.ToString().TrimEnd();
    }

    private static List<Dictionary<string, object?>> CollectJobContacts(JobResponse job, CustomerResponse? customer)
    {
        var contacts = new List<Dictionary<string, object?>>();
        if (job.MainContact is { } mc)
            contacts.Add(Contact(mc.Name, mc.Role, mc.Phone, mc.Email, tag: "Main Contact"));
        if (customer != null)
        {
            foreach (var p in customer.Persons ?? [])
                if (p.Id != job.MainContact?.Id)
                    contacts.Add(Contact(p.Name, p.Role, p.Phone, p.Email));
            if (customer.PrimaryPhone != null || customer.PrimaryEmail != null)
                contacts.Add(Contact(customer.Name, "Office", customer.PrimaryPhone, customer.PrimaryEmail));
        }
        return contacts;
    }

    private static List<Dictionary<string, object?>> CollectCustomerContacts(CustomerResponse customer)
    {
        var contacts = new List<Dictionary<string, object?>>();
        foreach (var p in customer.Persons ?? [])
            contacts.Add(Contact(p.Name, p.Role, p.Phone, p.Email));
        foreach (var c in customer.Contacts ?? [])
            contacts.Add(Contact(c.Label, null,
                c.Type == "phone" ? c.Value : null,
                c.Type == "email" ? c.Value : null));
        return contacts;
    }

    private static void AppendContacts(StringBuilder sb, List<Dictionary<string, object?>> contacts)
    {
        if (contacts.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("CONTACTS");
        foreach (var c in contacts)
        {
            var line = (string?)c["name"];
            if (c["role"] is string role && role.Length > 0) line += $" — {role}";
            if (c["tag"] is string tag && tag.Length > 0) line += $" ({tag})";
            sb.AppendLine(line);
            var details = new[] { (string?)c["phone"], (string?)c["email"] }
                .Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            if (details.Count > 0)
                sb.AppendLine("  " + string.Join(" · ", details));
        }
    }

    private static void AppendParts(StringBuilder sb, string header, List<JobItemResponse>? items)
    {
        if (items is not { Count: > 0 }) return;
        sb.AppendLine();
        sb.AppendLine(header);
        foreach (var i in items)
        {
            var partNumber = string.IsNullOrWhiteSpace(i.PartNumber) ? "" : $" ({i.PartNumber})";
            sb.AppendLine($"{i.Quantity:0.##}× {i.Name}{partNumber}");
            if (!string.IsNullOrWhiteSpace(i.Description))
                sb.AppendLine("  " + i.Description.Trim());
        }
    }

    private static Dictionary<string, object?> Contact(string name, string? role, string? phone, string? email, string? tag = null) => new()
    {
        ["name"] = name,
        ["role"] = role,
        ["phone"] = phone,
        ["email"] = email,
        ["tag"] = tag,
    };

    private static List<Dictionary<string, object?>> Parts(List<JobItemResponse>? items)
        => (items ?? []).Select(i => new Dictionary<string, object?>
        {
            ["name"] = i.Name,
            ["part_number"] = i.PartNumber,
            ["description"] = i.Description,
            ["quantity"] = i.Quantity.ToString("0.##"),
        }).ToList();
}
