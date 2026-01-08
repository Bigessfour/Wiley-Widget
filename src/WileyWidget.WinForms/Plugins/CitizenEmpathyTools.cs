#nullable enable

using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace WileyWidget.WinForms.Plugins
{
    /// <summary>
    /// Provides citizen-facing communication tools for utility management.
    /// Drafts empathetic, fact-based rate increase letters and professional complaint responses.
    /// Integrates JARVIS personality for engaging municipal finance communication.
    /// All operations are read-only; no mutations are permitted.
    /// </summary>
    public sealed class CitizenEmpathyTools
    {
        /// <summary>
        /// Initializes a new instance of the CitizenEmpathyTools plugin.
        /// </summary>
        public CitizenEmpathyTools()
        {
            // Stateless plugin; no dependencies required
        }

        /// <summary>
        /// Drafts a rate increase notification letter with empathetic tone and clear financial justification.
        /// Balances transparency about municipal constraints with professional communication.
        /// </summary>
        /// <param name="increasePct">Percentage increase in rates (e.g., 8.5 for 8.5%)</param>
        /// <param name="department">Department or fund name (e.g., "Water", "Sewer", "Solid Waste")</param>
        /// <param name="avgMonthlyImpact">Average monthly customer impact in dollars</param>
        /// <returns>Professional, empathetic rate increase letter as formatted text.</returns>
        [KernelFunction("draft_rate_increase_letter")]
        [Description("Draft a professional, empathetic rate increase notification letter with clear financial justification and typical customer impact.")]
        public Task<string> DraftRateIncreaseLetter(
            [Description("Percentage rate increase (e.g., 8.5 for 8.5%)")] decimal increasePct,
            [Description("Department or fund name (e.g., Water, Sewer, Solid Waste)")] string department,
            [Description("Average monthly customer impact in dollars")] decimal avgMonthlyImpact)
        {
            if (increasePct < 0)
                throw new ArgumentException("Increase percentage must be non-negative.", nameof(increasePct));
            if (string.IsNullOrWhiteSpace(department))
                throw new ArgumentException("Department name is required.", nameof(department));
            if (avgMonthlyImpact < 0)
                throw new ArgumentException("Monthly impact must be non-negative.", nameof(avgMonthlyImpact));

            var letterContent = GenerateRateIncreaseLetter(increasePct, department, avgMonthlyImpact);
            return Task.FromResult(letterContent);
        }

        /// <summary>
        /// Drafts a professional, de-escalating response to customer complaints.
        /// Acknowledges concerns, validates emotions, and provides constructive next steps.
        /// Maintains dignity and respect while addressing legitimate grievances.
        /// </summary>
        /// <param name="complaintText">The original complaint text from the citizen</param>
        /// <returns>Professional, empathetic response addressing the complaint.</returns>
        [KernelFunction("respond_to_complaint")]
        [Description("Draft a professional, de-escalating response to a citizen complaint that acknowledges concerns and provides constructive resolution.")]
        public Task<string> RespondToComplaint(
            [Description("The original complaint text or concern from the citizen")] string complaintText)
        {
            if (string.IsNullOrWhiteSpace(complaintText))
                throw new ArgumentException("Complaint text is required.", nameof(complaintText));

            var responseText = GenerateComplaintResponse(complaintText);
            return Task.FromResult(responseText);
        }

        #region Private Helpers

        /// <summary>
        /// Generates the rate increase letter content.
        /// </summary>
        private static string GenerateRateIncreaseLetter(decimal increasePct, string department, decimal avgMonthlyImpact)
        {
            var annualImpact = avgMonthlyImpact * 12;
            var today = DateTime.UtcNow.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

            var letter = $@"Dear Valued {department} Customer,

{today}

**IMPORTANT NOTICE: {department.ToUpper(System.Globalization.CultureInfo.InvariantCulture)} RATE ADJUSTMENT**

We are writing to inform you of an important change to your {department} service rates,
effective the next billing cycle.

**RATE INCREASE: {increasePct:F1}%**

After careful analysis of operational costs, infrastructure maintenance requirements, and
long-term sustainability needs, the municipal authority has determined that a rate adjustment
is necessary to continue providing reliable {department} services to our community.

**WHAT THIS MEANS FOR YOU:**

The typical residential customer will see an increase of approximately **${avgMonthlyImpact:F2}
per month** (or **${annualImpact:F2} per year**).

Example bill impact:
- Previous monthly bill: $100.00
- Estimated new monthly bill: ${100.00m * (1 + increasePct / 100):F2}
- Difference: ${avgMonthlyImpact:F2}

**WHY THIS INCREASE IS NECESSARY:**

1. **Infrastructure Maintenance**: Aging pipes, treatment facilities, and distribution systems
   require ongoing capital investment to prevent service disruptions and water quality issues.

2. **Regulatory Compliance**: State and federal environmental regulations require continuous
   upgrades to water quality standards and environmental protection measures.

3. **Operational Efficiency**: Staffing, energy, and chemical costs have increased significantly.
   We remain committed to lean operations while maintaining service excellence.

4. **System Reliability**: Strategic reserve building ensures we can respond to emergencies
   without service interruptions or emergency rate increases.

**OUR COMMITMENT TO YOU:**

- Every dollar of your rate contribution supports tangible service improvements
- We maintain transparent financial reporting to the community
- Assistance programs are available for income-qualified customers—please contact our office
- Your feedback matters: we welcome questions and comments about this adjustment

**NEXT STEPS:**

Questions? We're here to help:
- Call our customer service line: [Phone]
- Email: [Support Email]
- Visit: [Website]
- Office hours: [Hours]

We understand that any rate increase affects your household budget. We have worked diligently
to minimize this adjustment while ensuring sustainable, reliable service for years to come.
We appreciate your patience and continued partnership.

Thank you for your understanding.

Sincerely,

[Municipal Authority]
{department} Department
Water & Utility Services";

            return letter;
        }

        /// <summary>
        /// Generates a professional, de-escalating complaint response.
        /// </summary>
        private static string GenerateComplaintResponse(string complaintText)
        {
            // Analyze complaint sentiment and keywords
            var isUrgent = complaintText.Contains("immediately", StringComparison.OrdinalIgnoreCase)
                        || complaintText.Contains("urgent", StringComparison.OrdinalIgnoreCase)
                        || complaintText.Contains("emergency", StringComparison.OrdinalIgnoreCase);

            var isServiceIssue = complaintText.Contains("water", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("pressure", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("quality", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("outage", StringComparison.OrdinalIgnoreCase);

            var isBillingIssue = complaintText.Contains("bill", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("charge", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("fee", StringComparison.OrdinalIgnoreCase)
                              || complaintText.Contains("refund", StringComparison.OrdinalIgnoreCase);

            var issueCategory = isServiceIssue ? "service quality"
                             : isBillingIssue ? "billing"
                             : "general inquiry";

            var response = $@"Dear Valued Customer,

Thank you for bringing this matter to our attention. We take every concern seriously and
appreciate the opportunity to address your {issueCategory} complaint.

**WE HEAR YOU**

Your message has been received and reviewed by our management team. We understand your
frustration, and we want you to know that maintaining excellent service and fair billing
practices is our top priority.

**WHAT WE'RE DOING:**

1. **Immediate Review**: Your specific concern has been logged in our system and assigned
   to the appropriate department for thorough investigation.

2. **Timeline**: We will conduct a comprehensive review and follow up with you within 2-3
   business days with findings and proposed resolution.

3. **Direct Contact**: A designated representative will reach out to you directly to discuss
   your situation and answer any questions.

{(isUrgent ? @"4. **Priority Status**: Due to the urgent nature of your concern, we are
   escalating this to our emergency response team for expedited handling.
" : "")}

**YOUR NEXT STEPS:**

- Keep your account number and recent billing statement available for reference
- Be prepared to discuss specific details (dates, account information, etc.)
- Our team prefers email correspondence for documentation: [support@municipality.gov]
- If you need immediate assistance, call our emergency hotline: [24/7 Number]

**RESOLUTION OPTIONS:**

Depending on our findings, potential resolutions may include:
- Service inspection and corrective action
- Billing adjustment or credit
- System investigation and preventive measures
- Technical support or guidance

**YOUR SATISFACTION MATTERS**

We are committed to resolving this matter to your satisfaction. If you feel your initial
concern was not adequately addressed, you have the right to escalate to our Director of
Customer Services or file a formal complaint with the municipal authority.

**ESCALATION CONTACT:**
Director of Customer Services
[Phone] | [Email] | [Hours]

We appreciate your patience and partnership. Our utility service exists to serve the community,
and your feedback helps us improve continuously. We are confident that working together, we can
resolve this matter satisfactorily.

Respectfully,

[Municipal Authority]
Customer Service Department
Water & Utility Services

---
Reference ID: [AUTO-GENERATED ID]
Received: {DateTime.UtcNow:F}";

            return response;
        }

        #endregion
    }
}
