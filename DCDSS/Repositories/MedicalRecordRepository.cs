using DCDSS.Data;
using DCDSS.Entities;
using DCDSS.Repositories.Interfaces;

namespace DCDSS.Repositories;

public class MedicalRecordRepository : Repository<MedicalRecord>, IMedicalRecordRepository
{
    public MedicalRecordRepository(DcdssContext context) : base(context) { }
}
