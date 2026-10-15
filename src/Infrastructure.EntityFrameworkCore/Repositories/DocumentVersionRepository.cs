using Domain;

namespace Infrastructure.EntityFrameworkCore.Repositories;

class DocumentVersionRepository(
    ApplicationDbContext context) : BaseRepository<DocumentVersion>(context), IDocumentVersionRepository
{
}