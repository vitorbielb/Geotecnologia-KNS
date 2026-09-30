using GeotecnologiaKNS.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
#pragma warning disable CS8618

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext
        <ApplicationUser
        , ApplicationRole
        , string
        , IdentityUserClaim<string>
        , IdentityUserRole<string>
        , IdentityUserLogin<string>
        , IdentityRoleClaim<string>
        , IdentityUserToken<string>>
    {
        private readonly IUserContext _userContext;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserContext userContext)
            : base(options) => _userContext = userContext;

        public DbSet<Industria> Industrias { get; set; }
        public DbSet<Propriedade> Propriedades { get; set; }
        public DbSet<Produtor> Produtores { get; set; }
        public DbSet<PropriedadeArquivo> PropriedadesArquivos { get; set; }
        public DbSet<ProdutorArquivo> ProdutoresArquivos { get; set; }
        public DbSet<AnaliseArquivo> AnalisesArquivos { get; set; }
        public DbSet<Solicitacao> Solicitacao { get; set; }
        public DbSet<AnaliseAutomatica> AnalisesAutomaticas { get; set; }
        public DbSet<AnaliseOcorrencia> AnalisesOcorrencias { get; set; }
        public DbSet<PoliticaTenant> Politicas { get; set; }
        public DbSet<RegraTenant> Regras { get; set; }
        public DbSet<EventoDeUso> EventosDeUso { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // O Where não estava aqui porque no EF 6 todo tipo mapeado tinha
            // chave primária. A partir do EF 8 o modelo inclui tipos sem chave,
            // e FindPrimaryKey devolve nulo neles — o que derrubava a criação do
            // modelo inteiro com NullReferenceException.
            var keysProperties = modelBuilder.Model.GetEntityTypes()
                                             .Select(x => x.FindPrimaryKey())
                                             .Where(x => x is not null)
                                             .SelectMany(x => x!.Properties);
            foreach (var property in keysProperties)
            {
                property.ValueGenerated = ValueGenerated.OnAdd;
            }
       
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Propriedade>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Propriedade>()
                .HasMany(x => x.Documentos);

            // Busca por CAR é o caminho quente do cadastro e da reanálise.
            modelBuilder.Entity<Propriedade>()
                .HasIndex(x => x.CodigoCar);

            // O nome é opcional no formulário, mas nunca chega nulo ao banco:
            // quando vem em branco, o serviço do CAR gera um. Declarar
            // obrigatório aqui mantém a coluna NOT NULL sem tornar o campo
            // exigido na tela.
            modelBuilder.Entity<Propriedade>()
                .Property(x => x.NomePropriedade)
                .IsRequired();

            modelBuilder.Entity<Propriedade>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Propriedades)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Produtor>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Produtor>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Produtor>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Produtores)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Solicitacao>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Solicitacao>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Solicitacoes)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Documentos: os endpoints de download buscam pelo Id inteiro, então
            // sem filtro global era possível ler e apagar arquivo de outra
            // indústria só iterando o Id. Filtrar aqui vale para qualquer
            // consulta, inclusive as que ainda não foram escritas.
            modelBuilder.Entity<ProdutorArquivo>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<PropriedadeArquivo>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<AnaliseArquivo>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            // O filtro acima entra em toda consulta de documento; sem índice,
            // cada download varreria a tabela inteira.
            modelBuilder.Entity<ProdutorArquivo>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<PropriedadeArquivo>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<AnaliseArquivo>().HasIndex(x => x.TenantId);

            // Medição segue a mesma regra dos demais dados de inquilino: cada
            // indústria vê o próprio consumo, o administrador da aplicação vê todos.
            // A fila é consultada a cada poucos segundos pelo processador; sem
            // índice, cada consulta varreria todas as análises já feitas.
            modelBuilder.Entity<AnaliseAutomatica>()
                .HasIndex(x => new { x.Situacao, x.ProximaTentativaEm });

            modelBuilder.Entity<EventoDeUso>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<EventoDeUso>()
                .HasIndex(x => new { x.TenantId, x.Competencia });

            // Restrict, e não cascade: apagar uma indústria não pode levar junto
            // o histórico do que ela consumiu — é o lastro da fatura já emitida.
            modelBuilder.Entity<EventoDeUso>()
                .HasOne(e => e.Industria)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PoliticaTenant>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<PoliticaTenant>()
                .HasOne(e => e.Industria)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Uma política por indústria: mais de uma ativa tornaria ambíguo
            // qual regra produziu um laudo.
            modelBuilder.Entity<PoliticaTenant>()
                .HasIndex(x => x.TenantId)
                .IsUnique();

            modelBuilder.Entity<PoliticaTenant>()
                .HasMany(x => x.Regras)
                .WithOne(x => x.Politica)
                .HasForeignKey(x => x.PoliticaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtro espelhado no lado dependente, como em AnaliseOcorrencia:
            // sem ele, uma regra de outra indústria seria alcançável por
            // consulta direta ao conjunto.
            modelBuilder.Entity<RegraTenant>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.Politica!.TenantId == _userContext.TenantId);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasOne(e => e.Industria)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasMany(x => x.Ocorrencias)
                .WithOne(x => x.Analise)
                .HasForeignKey(x => x.AnaliseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtro espelhado no lado dependente: sem ele, uma ocorrência de
            // análise de outro tenant poderia ser lida por consulta direta.
            modelBuilder.Entity<AnaliseOcorrencia>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.Analise!.TenantId == _userContext.TenantId);

            modelBuilder.Entity<ApplicationUser>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Papéis internos são globais (TenantId 0) e precisam ser visíveis a
            // todas as indústrias — sem isso o administrador do cliente não
            // conseguia atribuir Solicitante nem Analista, porque o filtro exigia
            // igualdade com o tenant dele.
            //
            // O filtro trata só de inquilino; quem pode administrar papéis é
            // decisão da policy no controller. Misturar as duas coisas foi o que
            // produziu aquele defeito.
            //
            // Administrador fica de fora: é o papel de administração da própria
            // aplicação, e nenhum cliente deve poder concedê-lo.
            modelBuilder.Entity<ApplicationRole>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue ||
                                     _userContext.IsApplicationAdmin ||
                                     x.TenantId == _userContext.TenantId ||
                                     (x.TenantId == RoleGlobal.TenantId && x.Name != nameof(Infra.Roles.Administrador)));

            // O Identity cria índice único global em NormalizedName, o que impedia
            // duas indústrias de terem um papel com o mesmo nome — a segunda
            // recebia erro de chave duplicada por causa de um registro que ela
            // sequer podia ver. A unicidade passa a ser por inquilino.
            //
            // O índice da classe base precisa ser removido explicitamente: os dois
            // usam o nome RoleNameIndex e o EF recusa mapear colunas diferentes
            // para o mesmo índice.
            var tipoRole = modelBuilder.Entity<ApplicationRole>().Metadata;
            var propriedadeNome = tipoRole.FindProperty(nameof(ApplicationRole.NormalizedName));

            if (propriedadeNome is not null)
            {
                var indiceGlobal = tipoRole.FindIndex(propriedadeNome);

                if (indiceGlobal is not null)
                {
                    tipoRole.RemoveIndex(indiceGlobal);
                }
            }

            modelBuilder.Entity<ApplicationRole>()
                .HasIndex(x => new { x.TenantId, x.NormalizedName })
                .HasDatabaseName("RoleNameIndex")
                .IsUnique();

            modelBuilder.Entity<ApplicationRole>()
                .HasMany(x => x.Claims)
                .WithOne()
                .HasForeignKey(e => e.RoleId)
                .HasConstraintName("RoleId");

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(x => x.Claims)
                .WithOne()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("UserId");
        }
    }
}
