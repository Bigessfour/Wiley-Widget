using Microsoft.EntityFrameworkCore;
using WileyWidget.Data; // Assuming AppDbContext and entities like Enterprise are in WileyWidget.Data
using WileyWidget.Models;
using System.Linq;

namespace WileyWidget
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;

        public DatabaseSeeder(AppDbContext context)
        {
            _context = context;
        }

        public void Seed()
        {
            // Seed Enterprises
            if (!_context.Enterprises.Any())
            {
                _context.Enterprises.AddRange(
                    new Enterprise
                    {
                        Name = "Water",
                        CurrentRate = 45.00m,
                        MonthlyExpenses = 15000.00m,
                        CitizenCount = 245,
                        Notes = "Municipal water utility serving 245 residents"
                    },
                    new Enterprise
                    {
                        Name = "Trash",
                        CurrentRate = 25.00m,
                        MonthlyExpenses = 8000.00m,
                        CitizenCount = 245,
                        Notes = "Waste management services"
                    },
                    new Enterprise
                    {
                        Name = "Sewer",
                        CurrentRate = 35.00m,
                        MonthlyExpenses = 12000.00m,
                        CitizenCount = 245,
                        Notes = "Sewer system maintenance and operation"
                    }
                );
            }

            // Seed OverallBudgets
            if (!_context.OverallBudgets.Any())
            {
                var baseDate = DateTime.UtcNow.AddMonths(-12);
                for (int i = 0; i < 12; i++)
                {
                    _context.OverallBudgets.Add(new OverallBudget
                    {
                        SnapshotDate = baseDate.AddMonths(i),
                        TotalMonthlyRevenue = 25000.00m + (i * 500.00m),
                        TotalMonthlyExpenses = 22000.00m + (i * 300.00m),
                        TotalCitizensServed = 245,
                        AverageRatePerCitizen = 102.04m + (i * 2.00m),
                        Notes = $"Monthly budget snapshot for {baseDate.AddMonths(i):MMMM yyyy}",
                        IsCurrent = i == 11
                    });
                }
            }

            // Seed sample AI analysis results
            if (!_context.AiAnalysisResults.Any())
            {
                _context.AiAnalysisResults.AddRange(
                    new AiAnalysisResult
                    {
                        AnalysisType = "BudgetAnalysis",
                        InputHash = "SAMPLE_HASH_1",
                        AiResponse = "{\"analysis\": \"Sample AI analysis result\"}",
                        ProcessingTimeMs = 1500,
                        IsSuccessful = true,
                        Notes = "Sample analysis for demonstration"
                    },
                    new AiAnalysisResult
                    {
                        AnalysisType = "TrendAnalysis",
                        InputHash = "SAMPLE_HASH_2",
                        AiResponse = "{\"trends\": \"Expenses increasing 3% annually\"}",
                        ProcessingTimeMs = 1200,
                        IsSuccessful = true,
                        Notes = "Sample trend analysis"
                    }
                );
            }

            // Seed sample AI recommendations
            if (!_context.AiRecommendations.Any())
            {
                var waterEnterprise = _context.Enterprises.FirstOrDefault(e => e.Name == "Water");
                if (waterEnterprise != null)
                {
                    _context.AiRecommendations.Add(new AiRecommendation
                    {
                        EnterpriseId = waterEnterprise.Id,
                        RecommendationType = "RateHike",
                        Priority = "Medium",
                        RecommendationText = "Consider a 5% rate increase to cover rising infrastructure costs",
                        ExpectedImpact = 1250.00m,
                        ConfidenceLevel = 80,
                        Status = "Pending",
                        Notes = "Based on infrastructure aging analysis"
                    });
                }
            }

            // Seed sample audit entries
            if (!_context.AiAnalysisAudits.Any())
            {
                _context.AiAnalysisAudits.AddRange(
                    new AiAnalysisAudit
                    {
                        OperationType = "AnalysisStarted",
                        Description = "AI budget analysis initiated",
                        IsSuccessful = true,
                        Source = "WpfApplication"
                    },
                    new AiAnalysisAudit
                    {
                        OperationType = "AnalysisCompleted",
                        Description = "AI budget analysis completed successfully",
                        IsSuccessful = true,
                        DurationMs = 2500,
                        Source = "WpfApplication"
                    }
                );
            }

            _context.SaveChanges();
        }
        // ...existing code...
    }
}