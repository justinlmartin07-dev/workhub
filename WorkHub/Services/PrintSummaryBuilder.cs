using WorkHub.Models;

namespace WorkHub.Services;

// Builds the data models for the printable summaries and renders them through
// the HTML templates (fetched from the API by PrintTemplateService, so layout
// changes deploy server-side without a client update). The template token
// reference lives in the templates themselves: WorkHub.Api/Templates/*.html.
public static class PrintSummaryBuilder
{
    public static string BuildJobSummary(string template, JobResponse job, CustomerResponse? customer)
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
        var contacts = new List<Dictionary<string, object?>>();
        foreach (var p in customer.Persons ?? [])
            contacts.Add(Contact(p.Name, p.Role, p.Phone, p.Email));
        foreach (var c in customer.Contacts ?? [])
            contacts.Add(Contact(c.Label, null,
                c.Type == "phone" ? c.Value : null,
                c.Type == "email" ? c.Value : null));

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
            ["quantity"] = i.Quantity,
        }).ToList();
}
