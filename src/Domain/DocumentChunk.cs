namespace Domain;

public class DocumentChunk
{
    public long Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public long DocumentVersionId { get; protected set; }

    public DocumentVersion DocumentVersion
    {
        get;
        protected set
        {
            field = value;

            if (value != null)
            {
                DocumentVersionId = value.Id;
            }
        }
    } = null!;

    /// <summary>
    /// Gets or sets the local relative position of this chunk within its specific document version (0, 1, 2...).
    /// 
    /// WHY THIS IS CRUCIAL AND NOT REDUNDANT WITH THE PRIMARY KEY 'Id':
    /// 1. Document Reconstruction: Relational databases do not guarantee entry order. We order by this local index 
    ///    to structurally rebuild the text layout exactly as the human author wrote it.
    /// 2. Context Window Fetching: Enables precise adjacent mathematical queries (e.g., matching Index - 1 and Index + 1)
    ///    to serve surrounding paragraphs safely to the AI Agent tool.
    /// 3. Data Isolation: The primary key 'Id' increments globally across all system files and can scramble during 
    ///    multi-threaded bulk background jobs. This local index guarantees a gap-free sequence isolated entirely 
    ///    to this specific DocumentVersionId context boundary.
    /// </summary>
    public uint Index { get; set; }

    /// <summary>
    /// Gets or sets the vector embedding for this document.
    /// </summary>
    /// <remarks>
    /// **DEPRECATION & MODEL MIGRATION NOTICE**
    /// This column is scheduled to be replaced by a larger/new embedding model column.
    /// To migrate a live production system without downtime, follow this strict 5-step sequence:
    ///
    /// 1. SCHEMA UPDATE:
    ///    Add the new nullable column to your database:
    ///    `ALTER TABLE Documentchunks ADD COLUMN embedding_new vector(TARGET_DIMENSION);`
    ///
    /// 2. APPLICATION DUAL-WRITE (CURRENT STATE REQUIRED):
    ///    Update the application code to generate and write BOTH the old and new model
    ///    embeddings on all incoming INSERTs and UPDATEs. This ensures new data isn't missed
    ///    while the backfill process runs.
    ///    
    /// 3. BACKGROUND BACKFILL:
    ///    Run a background worker to query historical rows where `embedding_new IS NULL`,
    ///    generate the new embedding using the new model, and update those rows in batches.
    ///
    /// 4. CONCURRENT INDEXING (MUST BE SEPARATE):
    ///    Once the backfill is complete (verified via `COUNT(*) WHERE embedding_new IS NULL` equals 0),
    ///    create the new vector index.
    ///    
    ///    CRITICAL NOTE ON SEPARATION: Do NOT combine this index creation with Step 1 via standard EF Core Fluent API.
    ///    Even if 'embedding_new' is entirely empty, defining a standard index on an existing table forces
    ///    PostgreSQL to scan all existing rows under a strict ShareLock. This completely blocks all concurrent
    ///    live writes (INSERT/UPDATE/DELETE) to the rest of the table, causing a major production outage.
    ///    Furthermore, building an HNSW graph incrementally row-by-row during a massive backfill severely
    ///    degrades script performance and results in a highly fragmented, inaccurate vector graph.
    ///    
    ///    EF CORE TWO-MIGRATION EXECUTION STRATEGY:
    ///    • Migration 1: Add Column Only
    ///      Define the column in your DbContext without the index, generate the migration, and deploy it
    ///      to start your dual-writes and backfill.
    ///      
    ///    • Migration 2: Add Index Concurrently (Post-Backfill)
    ///      Once your backfill is finished, add the index configuration to your Fluent API:
    ///      <code>
    ///      protected override void OnModelCreating(ModelBuilder modelBuilder)
    ///      {
    ///          modelBuilder.Entity<DocumentVector>()
    ///              .HasIndex(d => d.EmbeddingNew)
    ///              .HasMethod("hnsw") // pgvector HNSW method
    ///              .HasOperators("vector_cosine_ops");
    ///      }
    ///      </code>
    ///      When you generate this second migration, manually edit the migration's Up method to build concurrently:
    ///      <code>
    ///      protected override void Up(MigrationBuilder migrationBuilder)
    ///      {
    ///          // Suppress the default transaction because CONCURRENTLY cannot run inside a transaction block
    ///          migrationBuilder.Sql("CREATE INDEX CONCURRENTLY idx_documents_embedding_new ON \"DocumentChunks\" USING hnsw (\"EmbeddingNew\" halfvec_cosine_ops);", suppressTransaction: true);
    ///      }
    ///      </code>
    /// 5. SWITCH & CLEANUP:
    ///    Flip application search queries to read from `embedding_new`. Once traffic is stable,
    ///    drop this old column and rename `embedding_new` to match this property's backing field name.
    /// </remarks>
    /// 
    public float[] Vector { get; set; } = [];

    public DocumentChunk() {}

    public DocumentChunk(
        string content, 
        DocumentVersion documentVersion, 
        uint index,
        float[] vector)
    {
        Content = content.Trim();
        DocumentVersion = documentVersion;
        Index = index;
        Vector = vector;
    }
}