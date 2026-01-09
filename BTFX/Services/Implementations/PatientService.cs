using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// Patient service implementation (temporary with mock data for Phase 2 testing)
/// </summary>
public class PatientService : IPatientService
{
    // Temporary mock data for testing - use static to share across instances
    private static readonly List<Patient> _mockPatients = InitializeMockPatients();
    private static readonly object _lock = new object();

    private static List<Patient> InitializeMockPatients()
    {
        return new List<Patient>
        {
            new Patient
            {
                Id = 1,
                Name = "Zhang San",
                Gender = Gender.Male,
                BirthDate = new DateTime(1980, 5, 15),
                Phone = "13800138001",
                IdNumber = "110101198005150011",
                Height = 175,
                Weight = 70,
                Address = "Beijing Chaoyang District",
                MedicalHistory = "None",
                Status = PatientStatus.Active,
                CreatedBy = 1,
                CreatedAt = DateTime.Now.AddDays(-30),
                UpdatedAt = DateTime.Now.AddDays(-30)
            },
            new Patient
            {
                Id = 2,
                Name = "Li Si",
                Gender = Gender.Female,
                BirthDate = new DateTime(1992, 8, 20),
                Phone = "13900139002",
                IdNumber = "110101199208200022",
                Height = 165,
                Weight = 55,
                Address = "Shanghai Pudong District",
                MedicalHistory = "Hypertension",
                Status = PatientStatus.Active,
                CreatedBy = 1,
                CreatedAt = DateTime.Now.AddDays(-25),
                UpdatedAt = DateTime.Now.AddDays(-25)
            },
            new Patient
            {
                Id = 3,
                Name = "Wang Wu",
                Gender = Gender.Male,
                BirthDate = new DateTime(1975, 3, 10),
                Phone = "13700137003",
                IdNumber = "110101197503100033",
                Height = 180,
                Weight = 85,
                Address = "Guangzhou Tianhe District",
                MedicalHistory = "Diabetes",
                Status = PatientStatus.Active,
                CreatedBy = 2,
                CreatedAt = DateTime.Now.AddDays(-20),
                UpdatedAt = DateTime.Now.AddDays(-20)
            },
            new Patient
            {
                Id = 4,
                Name = "Zhao Liu",
                Gender = Gender.Female,
                BirthDate = new DateTime(1988, 11, 25),
                Phone = "13600136004",
                IdNumber = "110101198811250044",
                Height = 160,
                Weight = 50,
                Address = "Shenzhen Nanshan District",
                Status = PatientStatus.Active,
                CreatedBy = 2,
                CreatedAt = DateTime.Now.AddDays(-15),
                UpdatedAt = DateTime.Now.AddDays(-15)
            },
            new Patient
            {
                Id = 5,
                Name = "Chen Qi",
                Gender = Gender.Male,
                BirthDate = new DateTime(1995, 6, 30),
                Phone = "13500135005",
                IdNumber = "110101199506300055",
                Height = 172,
                Weight = 68,
                Address = "Chengdu Jinjiang District",
                Status = PatientStatus.Active,
                CreatedBy = 1,
                CreatedAt = DateTime.Now.AddDays(-10),
                UpdatedAt = DateTime.Now.AddDays(-10)
            }
        };
    }

    /// <inheritdoc/>
    public Task<List<Patient>> GetAllPatientsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_mockPatients.Where(p => p.Status == PatientStatus.Active).ToList());
        }
    }

    /// <inheritdoc/>
    public Task<(List<Patient> Patients, int TotalCount)> GetPatientsPagedAsync(int pageIndex, int pageSize, string? searchText = null)
    {
        lock (_lock)
        {
            var query = _mockPatients.Where(p => p.Status == PatientStatus.Active);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var searchLower = searchText.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchLower) ||
                    (p.Phone != null && p.Phone.Contains(searchLower)) ||
                    (p.IdNumber != null && p.IdNumber.ToLower().Contains(searchLower)));
            }

            var totalCount = query.Count();
            var patients = query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult((patients, totalCount));
        }
    }

    /// <inheritdoc/>
    public Task<Patient?> GetPatientByIdAsync(int id)
    {
        lock (_lock)
        {
            var patient = _mockPatients.FirstOrDefault(p => p.Id == id && p.Status == PatientStatus.Active);
            return Task.FromResult(patient);
        }
    }

    /// <inheritdoc/>
    public Task<int> AddPatientAsync(Patient patient)
    {
        lock (_lock)
        {
            patient.Id = _mockPatients.Any() ? _mockPatients.Max(p => p.Id) + 1 : 1;
            patient.CreatedAt = DateTime.Now;
            patient.UpdatedAt = DateTime.Now;
            patient.Status = PatientStatus.Active;
            _mockPatients.Add(patient);
            return Task.FromResult(patient.Id);
        }
    }

    /// <inheritdoc/>
    public Task<bool> UpdatePatientAsync(Patient patient)
    {
        lock (_lock)
        {
            var existing = _mockPatients.FirstOrDefault(p => p.Id == patient.Id);
            if (existing != null)
            {
                var index = _mockPatients.IndexOf(existing);
                patient.UpdatedAt = DateTime.Now;
                _mockPatients[index] = patient;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeletePatientAsync(int id)
    {
        lock (_lock)
        {
            var patient = _mockPatients.FirstOrDefault(p => p.Id == id);
            if (patient != null)
            {
                // Logical delete
                patient.Status = PatientStatus.Deleted;
                patient.UpdatedAt = DateTime.Now;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<List<Patient>> SearchPatientsAsync(string searchText)
    {
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return Task.FromResult(_mockPatients.Where(p => p.Status == PatientStatus.Active).ToList());
            }

            var searchLower = searchText.ToLower();
            var results = _mockPatients.Where(p =>
                p.Status == PatientStatus.Active &&
                (p.Name.ToLower().Contains(searchLower) ||
                 (p.Phone != null && p.Phone.Contains(searchLower)) ||
                 (p.IdNumber != null && p.IdNumber.ToLower().Contains(searchLower))))
                .ToList();

            return Task.FromResult(results);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsPatientExistsAsync(string phone, int? excludeId = null)
    {
        lock (_lock)
        {
            var exists = _mockPatients.Any(p =>
                p.Status == PatientStatus.Active &&
                p.Phone == phone &&
                (!excludeId.HasValue || p.Id != excludeId.Value));
            return Task.FromResult(exists);
        }
    }
}
