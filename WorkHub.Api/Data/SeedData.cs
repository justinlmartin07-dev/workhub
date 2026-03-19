using Microsoft.EntityFrameworkCore;
using WorkHub.Api.Models;
using WorkHub.Api.Services;

namespace WorkHub.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(WorkHubDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        var rng = new Random(42); // deterministic seed

        // --- Users ---
        var users = new[]
        {
            new User
            {
                Id = Guid.NewGuid(), Email = "admin@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Admin User", CreatedAt = now,
            },
            new User
            {
                Id = Guid.NewGuid(), Email = "user1@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Team Member 1", CreatedAt = now,
            },
            new User
            {
                Id = Guid.NewGuid(), Email = "user2@workhub.app",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                Name = "Team Member 2", CreatedAt = now,
            },
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        // --- Inventory Items (100) ---
        var partPrefixes = new[] { "HVAC", "PLM", "ELC", "GEN", "FLR", "RF", "INS", "DRW", "PNT", "HRD" };
        var partNames = new[]
        {
            "Capacitor", "Contactor", "Thermostat", "Filter", "Relay", "Fuse", "Valve",
            "Coupling", "Elbow", "Tee", "Reducer", "Fitting", "Adapter", "Gasket",
            "Breaker", "Outlet", "Switch", "Wire Nut", "Junction Box", "Conduit",
            "Bolt Set", "Anchor", "Bracket", "Hanger", "Clamp", "Strap", "Screw Pack",
            "Tile", "Grout", "Adhesive", "Sealant", "Caulk", "Primer", "Paint Gallon",
            "Shingle Bundle", "Flashing", "Vent Cap", "Membrane Roll", "Drip Edge", "Ridge Vent",
            "R-19 Batt", "R-30 Batt", "Foam Board", "Spray Can", "Vapor Barrier",
            "Drywall Sheet", "Joint Compound", "Tape Roll", "Corner Bead", "Texture Spray",
            "Pipe 1/2\"", "Pipe 3/4\"", "Pipe 1\"", "PVC Cement", "Teflon Tape",
            "Fan Motor", "Blower Wheel", "Compressor", "Expansion Valve", "Refrigerant Can",
            "Disconnect Box", "Transformer", "Ignitor", "Flame Sensor", "Gas Valve",
            "Copper Tubing 3/8\"", "Copper Tubing 1/2\"", "Line Set 25ft", "Insulation Wrap", "Condensate Pump",
            "LED Bulb Pack", "Ballast", "Photocell", "Motion Sensor", "Dimmer Switch",
            "P-Trap", "Wax Ring", "Supply Line", "Shutoff Valve", "Drain Assembly",
            "Thermocouple", "Pilot Assembly", "Heat Exchanger", "Burner Assembly", "Draft Inducer",
            "Flex Duct 6\"", "Flex Duct 8\"", "Register 4x10", "Register 6x12", "Return Grille",
            "Smoke Detector", "CO Detector", "Fire Extinguisher", "Exit Sign", "Emergency Light",
            "Concrete Mix", "Rebar 4ft", "Wire Mesh", "Form Board", "Expansion Joint",
            "Sandpaper Pack", "Wood Filler", "Stain Quart", "Polyurethane", "Wood Glue",
        };
        var inventoryItems = new List<InventoryItem>();
        for (var i = 0; i < 100; i++)
        {
            var prefix = partPrefixes[i / 10];
            inventoryItems.Add(new InventoryItem
            {
                Id = Guid.NewGuid(),
                Name = partNames[i],
                Description = $"Standard {partNames[i].ToLower()} for field service use",
                PartNumber = $"{prefix}-{1000 + i}",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        db.InventoryItems.AddRange(inventoryItems);
        await db.SaveChangesAsync();

        // --- Customers (200) ---
        var firstNames = new[] { "James", "Mary", "Robert", "Patricia", "John", "Jennifer", "Michael", "Linda", "David", "Elizabeth", "William", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Christopher", "Karen", "Charles", "Lisa", "Daniel", "Nancy", "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra", "Donald", "Ashley", "Steven", "Kimberly", "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores" };
        var streets = new[] { "Main St", "Oak Ave", "Elm Dr", "Cedar Ln", "Pine Rd", "Maple Blvd", "Walnut St", "Birch Ct", "Spruce Pl", "Ash Way", "Cherry Ln", "Willow Dr", "Poplar Ave", "Hickory St", "Sycamore Rd", "Magnolia Blvd", "Dogwood Ct", "Juniper Ln", "Holly Dr", "Cypress Ave" };
        var cities = new[] { "Springfield", "Riverside", "Fairview", "Georgetown", "Clinton", "Madison", "Franklin", "Greenville", "Bristol", "Oakland", "Salem", "Arlington", "Burlington", "Manchester", "Milton", "Newport", "Chester", "Richmond", "Kingston", "Lexington" };
        var states = new[] { "AL", "AZ", "CA", "CO", "FL", "GA", "IL", "IN", "KY", "LA", "MD", "MI", "MN", "MO", "NC", "NJ", "NY", "OH", "PA", "TX", "VA", "WA" };
        var contactLabels = new[] { "Mobile", "Home", "Work", "Office", "Main" };

        var customers = new List<Customer>();
        var allContacts = new List<CustomerContact>();

        for (var i = 0; i < 200; i++)
        {
            var first = firstNames[rng.Next(firstNames.Length)];
            var last = lastNames[rng.Next(lastNames.Length)];
            var street = $"{rng.Next(100, 9999)} {streets[rng.Next(streets.Length)]}";
            var city = cities[rng.Next(cities.Length)];
            var state = states[rng.Next(states.Length)];
            var zip = $"{rng.Next(10000, 99999)}";
            var address = $"{street}\n{city}, {state} {zip}";
            var createdAt = now.AddDays(-rng.Next(30, 365));

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = $"{first} {last}",
                Address = address,
                NormalizedAddress = AddressNormalizer.Normalize(address),
                Notes = rng.Next(3) == 0 ? $"Preferred contact: {contactLabels[rng.Next(contactLabels.Length)]}. Gate code {rng.Next(1000, 9999)}." : null,
                CreatedBy = users[rng.Next(users.Length)].Id,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
            customers.Add(customer);

            // 1-3 phone contacts
            var phoneCount = rng.Next(1, 4);
            for (var p = 0; p < phoneCount; p++)
            {
                allContacts.Add(new CustomerContact
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Type = "phone",
                    Label = contactLabels[p % contactLabels.Length],
                    Value = $"({rng.Next(200, 999)}) {rng.Next(200, 999)}-{rng.Next(1000, 9999)}",
                    IsPrimary = p == 0,
                    CreatedAt = createdAt,
                });
            }

            // 0-2 email contacts
            var emailCount = rng.Next(0, 3);
            for (var e = 0; e < emailCount; e++)
            {
                allContacts.Add(new CustomerContact
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    Type = "email",
                    Label = e == 0 ? "Personal" : "Work",
                    Value = $"{first.ToLower()}.{last.ToLower()}{(e > 0 ? e.ToString() : "")}@example.com",
                    IsPrimary = e == 0,
                    CreatedAt = createdAt,
                });
            }
        }
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
        db.CustomerContacts.AddRange(allContacts);
        await db.SaveChangesAsync();

        // --- Jobs (500) ---
        var jobTitles = new[] {
            "AC Repair", "Furnace Inspection", "Water Heater Install", "Duct Cleaning",
            "Electrical Panel Upgrade", "Outlet Installation", "Ceiling Fan Install",
            "Roof Leak Repair", "Gutter Replacement", "Siding Repair",
            "Plumbing Leak Fix", "Toilet Replacement", "Faucet Install", "Drain Cleaning",
            "Insulation Upgrade", "Window Replacement", "Door Install", "Drywall Repair",
            "Paint Interior", "Paint Exterior", "Fence Repair", "Deck Staining",
            "Concrete Patching", "Tile Repair", "Flooring Install", "Carpet Replacement",
            "Smoke Detector Install", "Thermostat Upgrade", "Generator Hookup",
            "Landscape Lighting", "Security Camera Install", "Garage Door Repair",
            "Appliance Hookup", "Gas Line Repair", "Sump Pump Install",
            "Bathroom Remodel", "Kitchen Backsplash", "Cabinet Install",
            "Pressure Washing", "Tree Trimming",
        };
        var statuses = new[] { "Pending", "In Progress", "Complete" };
        var priorities = new[] { "Low", "Normal", "Normal", "Normal", "High" }; // weighted toward Normal
        var scopeNotes = new[]
        {
            "Customer reports intermittent issues. Check all connections.",
            "Follow up from previous visit. Parts should be on-site.",
            "New installation per customer request. Verify measurements first.",
            "Emergency call — customer has no heat/cooling.",
            "Annual maintenance visit. Full system inspection required.",
            "Customer wants estimate before proceeding with work.",
            "Second opinion requested. Previous contractor quoted high.",
            "Insurance claim work. Document everything with photos.",
            "Warranty repair — check serial numbers before starting.",
            "Tenant-occupied unit. Coordinate access with property manager.",
            null, null, null, // some jobs have no scope notes
        };

        var jobs = new List<Job>();
        var allNotes = new List<JobNote>();
        var allJobInventory = new List<JobInventory>();
        var allAdhocItems = new List<JobAdhocItem>();

        for (var i = 0; i < 500; i++)
        {
            var customer = customers[rng.Next(customers.Count)];
            var createdAt = customer.CreatedAt.AddDays(rng.Next(0, 90));
            if (createdAt > now) createdAt = now.AddDays(-rng.Next(1, 10));
            var status = statuses[rng.Next(statuses.Length)];

            var job = new Job
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Title = jobTitles[rng.Next(jobTitles.Length)],
                Status = status,
                Priority = priorities[rng.Next(priorities.Length)],
                ScopeNotes = scopeNotes[rng.Next(scopeNotes.Length)],
                CreatedBy = users[rng.Next(users.Length)].Id,
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddDays(rng.Next(0, 14)),
            };
            jobs.Add(job);

            // 0-5 notes per job
            var noteCount = rng.Next(0, 6);
            var noteTexts = new[]
            {
                "Arrived on-site, assessed the situation.",
                "Parts ordered, waiting for delivery.",
                "Work completed, tested and verified.",
                "Customer not home, left voicemail.",
                "Need to return with additional materials.",
                "Spoke with customer about upgrade options.",
                "Found additional issue — notified customer.",
                "Waiting on permit approval.",
                "Subcontractor scheduled for next week.",
                "Final walkthrough done, customer signed off.",
                "Took before/after photos.",
                "Weather delay — rescheduled for tomorrow.",
            };
            for (var n = 0; n < noteCount; n++)
            {
                allNotes.Add(new JobNote
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Content = noteTexts[rng.Next(noteTexts.Length)],
                    CreatedBy = users[rng.Next(users.Length)].Id,
                    CreatedAt = createdAt.AddHours(rng.Next(1, 200)),
                });
            }

            // 0-4 inventory items per job
            var invCount = rng.Next(0, 5);
            var usedItemIds = new HashSet<Guid>();
            for (var j = 0; j < invCount; j++)
            {
                var inv = inventoryItems[rng.Next(inventoryItems.Count)];
                if (!usedItemIds.Add(inv.Id)) continue; // skip duplicates
                allJobInventory.Add(new JobInventory
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    InventoryItemId = inv.Id,
                    Quantity = rng.Next(1, 8),
                    ListType = rng.Next(3) == 0 ? "to_order" : "used",
                    CreatedAt = createdAt,
                });
            }

            // 0-2 adhoc items per job
            var adhocCount = rng.Next(0, 3);
            var adhocNames = new[] { "Custom bracket", "Specialty fitting", "Non-stock filter", "OEM part", "Customer-supplied material", "Misc hardware", "Special order valve", "Custom cut pipe" };
            for (var a = 0; a < adhocCount; a++)
            {
                allAdhocItems.Add(new JobAdhocItem
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = adhocNames[rng.Next(adhocNames.Length)],
                    Quantity = rng.Next(1, 5),
                    ListType = rng.Next(3) == 0 ? "to_order" : "used",
                    CreatedAt = createdAt,
                });
            }
        }

        db.Jobs.AddRange(jobs);
        await db.SaveChangesAsync();
        db.JobNotes.AddRange(allNotes);
        await db.SaveChangesAsync();
        db.JobInventories.AddRange(allJobInventory);
        db.JobAdhocItems.AddRange(allAdhocItems);
        await db.SaveChangesAsync();

        // --- Calendar Events (150) ---
        var eventTitles = new[] {
            "Site Visit", "Follow-up Inspection", "Customer Consultation", "Team Meeting",
            "Parts Pickup", "Permit Inspection", "Emergency Call", "Routine Maintenance",
            "Estimate Walkthrough", "Final Walkthrough", "Training Session", "Safety Review",
            "Equipment Delivery", "Subcontractor Meeting", "Warranty Check",
        };
        var events = new List<CalendarEvent>();
        var allAssignments = new List<CalendarEventAssignment>();

        for (var i = 0; i < 150; i++)
        {
            var daysOffset = rng.Next(-60, 60);
            var hour = rng.Next(7, 17);
            var startTime = now.Date.AddDays(daysOffset).AddHours(hour);
            var durationHours = rng.Next(1, 5);
            var customer = rng.Next(3) > 0 ? customers[rng.Next(customers.Count)] : null;
            var job = customer != null && rng.Next(2) == 0
                ? jobs.FirstOrDefault(j => j.CustomerId == customer.Id)
                : null;

            var evt = new CalendarEvent
            {
                Id = Guid.NewGuid(),
                Title = eventTitles[rng.Next(eventTitles.Length)],
                Description = rng.Next(2) == 0 ? $"Scheduled for {customer?.Name ?? "internal"}. Duration: {durationHours}h." : null,
                StartTime = startTime,
                EndTime = startTime.AddHours(durationHours),
                ReminderMinutes = rng.Next(4) == 0 ? 30 : null,
                CustomerId = customer?.Id,
                JobId = job?.Id,
                CreatedBy = users[rng.Next(users.Length)].Id,
                CreatedAt = startTime.AddDays(-rng.Next(1, 14)),
            };
            events.Add(evt);

            // 1-3 user assignments
            var assignCount = rng.Next(1, 4);
            var assignedUsers = new HashSet<Guid>();
            for (var a = 0; a < assignCount; a++)
            {
                var user = users[rng.Next(users.Length)];
                if (!assignedUsers.Add(user.Id)) continue;
                allAssignments.Add(new CalendarEventAssignment
                {
                    Id = Guid.NewGuid(),
                    CalendarEventId = evt.Id,
                    UserId = user.Id,
                    CreatedAt = evt.CreatedAt,
                });
            }
        }

        db.CalendarEvents.AddRange(events);
        await db.SaveChangesAsync();
        db.CalendarEventAssignments.AddRange(allAssignments);
        await db.SaveChangesAsync();
    }
}
