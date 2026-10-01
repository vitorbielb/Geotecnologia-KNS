# Camadas de referência

As camadas contra as quais o perímetro do imóvel é cruzado. Cada regra do
protocolo examina um tipo de camada; **regra sem camada carregada não é
avaliada**, e o laudo diz isso explicitamente em vez de liberar por omissão.

Todos os comandos abaixo pressupõem a variável de ambiente:

```bash
export ConnectionStrings__Geo="Host=localhost;Port=5432;Database=geotecnologiakns_geo;Username=geo;Password=geo_local_dev"
```

Para conferir o que está carregado:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- camadas
```

---

## Situação

| Regra | Tipo de camada | Fonte | Situação |
|---|---|---|---|
| EMB-001 | EmbargoAmbiental | IBAMA Dados Abertos | carregada |
| DES-001 | DesmatamentoConsolidado | PRODES / TerraBrasilis | carregada |
| TI-001 | TerraIndigena | FUNAI Geoserver | carregada |
| ALE-001 | AlertaDesmatamento | DETER / TerraBrasilis | carregada |
| UC-001 | UnidadeConservacao | CNUC/MMA | carregada |
| ASS-001 | AssentamentoRural | INCRA | carregada |
| QUI-001 | TerritorioQuilombola | IBGE | carregada |
| OUT-001 | OutroPerimetro | definido pela indústria | — |

---

## Embargos ambientais (IBAMA)

Portal: <https://dadosabertos.ibama.gov.br/dataset/fiscalizacao-termo-de-embargo>
Recurso: **Termos de embargo** (`termo_de_embargo.csv`, ~209 MB)

```bash
curl -L -o termo_de_embargo.csv \
  "https://stibamadadosabertosprd.blob.core.windows.net/dados-abertos/dados/TERMOS_DE_EMBARGO/TERMO_EMBARGO/termo_de_embargo.csv"

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- \
  importar-embargo --arquivo termo_de_embargo.csv
```

Observações que custaram tempo para descobrir:

- **Não use o arquivo de coordenadas** que o portal oferece à parte. Ele traz
  vértices soltos, um por linha, e um terço dos polígonos tem menos de três
  pontos — reconstruir dali produz áreas erradas. A coluna
  `GEOM_AREA_EMBARGADA` do arquivo de termos já vem com o polígono fechado.
- O link para `termo_de_embargo.csv` na página do portal aponta para um caminho
  que devolve 404. O caminho correto é o do comando acima.
- O arquivo é publicado em **Latin1**, não UTF-8.
- Embargos cancelados (`SIT_CANCELADO = S`) são descartados na importação.

Última carga de referência: 57.843 polígonos de 116.564 registros, em 32s.

### Lista restritiva por CPF/CNPJ

O mesmo arquivo alimenta uma segunda estrutura: `geo.restricao_documento`,
consultada pela regra EMB-002 durante a análise.

Ela existe porque **quase metade dos termos do IBAMA não tem área delimitada** —
cerca de 50 mil embargos que nenhum cruzamento de polígono encontra. E resolve o
caso que a geografia nunca resolveria: o produtor cujo imóvel está limpo, mas
que responde por embargo em outra fazenda.

Última carga: 100.691 registros, 81.179 pessoas distintas, 49.901 sem área.

Termos lavrados contra autuado não identificado vêm com documento 00000000000;
esses são recusados na normalização, senão qualquer produtor cadastrado com
documento de preenchimento casaria com dezenas de embargos alheios.

---

## Terras indígenas (FUNAI)

```bash
curl -L -o tis.zip \
  "https://geoserver.funai.gov.br/geoserver/Funai/ows?service=WFS&version=1.0.0&request=GetFeature&typeName=Funai:tis_poligonais&maxFeatures=5000&outputFormat=SHAPE-ZIP"
unzip tis.zip

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- importar-camada \
  --arquivo tis_poligonais.shp \
  --chave terra-indigena-funai \
  --nome "Terras Indígenas" \
  --tipo TerraIndigena \
  --origem "FUNAI — Geoserver (tis_poligonais)"
```

- O `maxFeatures` **é obrigatório**: sem ele o nginx da FUNAI devolve 403. O
  valor 5000 cobre a base inteira, que tem 665 polígonos.
- **A origem é intermitente.** Em 01/10/2026 todo `GetFeature` passou a
  devolver 403 — com `maxFeatures`, sem ele, em `/ows` e em `/wfs`, tanto
  em SHAPE-ZIP quanto em GeoJSON — enquanto o `GetCapabilities` no mesmo
  servidor continuava respondendo 200. Dois dias antes a carga tinha funcionado.
  É bloqueio do nginx deles, não erro de requisição. Quando acontece, a camada
  simplesmente continua na versão anterior e a recarga tenta de novo na próxima
  conferência; a falha aparece no registro, não no laudo.
- O arquivo vem em SIRGAS 2000 (EPSG 4674). Para o Brasil a diferença para
  WGS 84 é centimétrica e irrelevante na escala de um imóvel rural.
- O rótulo inclui a fase (`Regularizada`, `Declarada`, `Em Estudo`...), porque
  elas restringem coisas juridicamente diferentes. **Todas são carregadas** —
  se a política da indústria quiser considerar apenas as regularizadas, isso é
  ajuste de regra, não de carga.

---

## Alertas de desmatamento (DETER)

```bash
CORTE=$(date -d "-12 months" +%Y-%m-%d)

curl -L -G -o deter.zip "https://terrabrasilis.dpi.inpe.br/geoserver/deter-amz/ows" \
  --data-urlencode "service=WFS" --data-urlencode "version=1.0.0" \
  --data-urlencode "request=GetFeature" --data-urlencode "typeName=deter-amz:deter_amz" \
  --data-urlencode "outputFormat=SHAPE-ZIP" \
  --data-urlencode "CQL_FILTER=view_date >= '$CORTE'"
unzip deter.zip

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- importar-camada \
  --arquivo deter_amz.shp \
  --chave deter-amazonia \
  --nome "DETER Amazônia (12 meses)" \
  --tipo AlertaDesmatamento \
  --origem "INPE/TerraBrasilis (deter_amz)"
```

- O filtro por data **é necessário**: a camada completa acumula anos de alertas
  e a requisição sem filtro estoura em 504.
- Doze meses é escolha de produto, não limitação: alerta é desmatamento
  recente e não consolidado. O histórico consolidado já vem do PRODES, e
  carregar tudo duplicaria as mesmas áreas no laudo.
- Existe também `deter-cerrado-nb:deter_cerrado`, para quem opera fora da
  Amazônia.

Última carga de referência: 20.541 alertas.

---

## Recarga automática

As camadas se recarregam sozinhas. A aplicação confere de hora em hora quais
passaram do prazo e baixa só essas, direto da origem. Nada precisa ser agendado
no sistema operacional.

Isso existe porque o modo de falhar de um sistema desses não é parar: é
continuar funcionando com dados velhos. A base de embargos de hoje vira a base
de dezembro, todo termo lavrado nesse meio-tempo passa despercebido, e o laudo
segue dizendo "nenhuma sobreposição encontrada" com a mesma confiança de
sempre. Nenhum erro aparece em lugar nenhum.

| Camada | Frequência da origem | Recarga |
|---|---|---|
| Embargos IBAMA (área e documento) | diária | semanal |
| DETER | quase diária | semanal |
| Terras indígenas | esporádica | trimestral |
| Unidades de conservação | esporádica | trimestral |
| Assentamentos | esporádica | trimestral |
| Territórios quilombolas | esporádica | semestral |
| PRODES | anual | anual |
| Cadastro de Empregadores (MTE) | a cada poucos meses | mensal |

De onde vem cada uma e de quanto em quanto tempo está em
`src/GeotecnologiaKNS.Geo/Ingestao/CatalogoDeFontes.cs`, não aqui: um endereço
que só existe no runbook só é usado quando alguém lembra de abrir o runbook.

Para ver a idade do que está carregado:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- camadas
```

```
CHAVE                      TIPO                       FEIÇÕES  ATUALIZADA     SITUAÇÃO
embargo-ibama              EmbargoAmbiental            57.843  hoje           vence em 7 dias
deter-amazonia             AlertaDesmatamento          20.541  há 2 dias      vence em 5 dias
```

Para forçar na mão — uma camada, as vencidas, ou todas:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --chave embargo-ibama
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --todas
```

O comando devolve código de saída diferente de zero quando alguma origem falha,
para quem preferir agendar por fora. Nesse caso, desligue o serviço interno com
`Camadas:RecargaAutomatica = false` — em desenvolvimento ele já vem desligado,
senão cada subida da aplicação baixaria 209 MB do IBAMA.

### O que impede a recarga de estragar a base

Recarga desassistida só é segura por causa de três coisas que vêm antes dela.

**Troca versionada.** A carga nova é gravada ao lado da que está no ar, com o
número de versão seguinte, e a análise continua enxergando a antiga. Só no fim,
depois de conferida, a camada passa a apontar para a nova. Uma quebra no meio
— rede caindo, processo morto, banco reiniciado — não deixa a camada pela
metade, e camada pela metade é pior que camada ausente: ela *parece* carregada.

**Conferência de tamanho.** Uma carga que vem vazia, ou com menos da metade do
que havia antes, é recusada e a anterior é mantida. Metade é folgado de
propósito: embargo revogado e unidade de conservação extinta são dezenas, não
dezenas de milhares. Sem isso, um CSV truncado na origem apagaria 57 mil
embargos bons, gravaria duzentos no lugar, e todo imóvel passaria a ser
liberado.

**Trava no PostgreSQL.** Duas recargas da mesma camada ao mesmo tempo — o
serviço interno e alguém na linha de comando — gravariam as duas na mesma versão
seguinte, e a contagem final sairia somada: passaria pela conferência sem
ninguém notar.

As duas estruturas que saem do arquivo do IBAMA — a camada geográfica e a lista
por CPF/CNPJ — são conferidas juntas e trocadas juntas. Publicar uma e recusar a
outra deixaria EMB-001 e EMB-002 falando de arquivos diferentes, e elas se
apoiam uma na outra para não contar o mesmo embargo duas vezes.

Quando uma recarga é recusada, o registro diz o que aconteceu e o que ficou no
ar:

```
A carga trouxe 2 feições contra 57.843 da versão anterior. Uma queda dessa ordem
costuma ser arquivo truncado na origem, não redução real. A versão anterior foi
mantida.
```

Sobras de uma carga que não chegou a ser publicada aparecem no `diagnostico` e
são limpas pela recarga seguinte. Elas não entram em análise nenhuma.

A recarga substitui a camada inteira: feição que saiu da fonte desaparece do
sistema, e nada é duplicado. Laudos já emitidos não mudam — eles guardam o
retrato das camadas e das regras usadas na execução.

### PRODES e o ano de referência

Recarregar `prodes-amazonia-2024` traz a revisão de 2024, não o ano seguinte.
Isso é de propósito: trocar o ano por baixo mudaria o significado dos laudos já
emitidos. Um ano novo é camada nova, com entrada própria no catálogo.

---

## Unidades de conservação (CNUC/MMA)

O CNUC não publica shapefile: o portal é uma aplicação JavaScript e o download
sai do backend em GeoJSON, gerado por `ogr2ogr` sobre um MapServer interno.

```bash
curl -X POST "https://cnuc-backend.mma.gov.br/api/v1/downloadGeo" \
  -H "Content-Type: application/json" \
  -d '{"map":"/var/www/storage/app/mapfiles/ucs.map","name":"ucs","typename":"ucs_selected","ucIds":"null","format":"GeoJSON"}' \
  -o ucs.zip
unzip ucs.zip

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- importar-geojson \
  --arquivo ucs.GeoJSON \
  --chave unidade-conservacao-cnuc \
  --nome "Unidades de Conservação" \
  --tipo UnidadeConservacao \
  --origem "MMA — CNUC (ucs_selected)"
```

- O campo `format` **é obrigatório**. Sem ele o backend devolve 500 com o erro
  do `ogr2ogr` (`Unable to find driver`).
- `ESRI Shapefile` é recusado; só `GeoJSON` funciona.
- O arquivo tem 227 MB, por isso a importação é em fluxo — carregá-lo como
  texto passaria de 450 MB em memória só para começar.
- O rótulo inclui a categoria de manejo: "PARQUE ESTADUAL SUMAÚMA (Parque)".

Última carga de referência: 3.509 unidades, nenhuma descartada, em 18s.

---

## Projetos de assentamento (INCRA)

```bash
curl -L -o assentamentos.zip \
  "https://certificacao.incra.gov.br/csv_shp/zip/Assentamento%20Brasil.zip"
unzip assentamentos.zip

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- importar-camada \
  --arquivo "Assentamento Brasil.shp" \
  --chave assentamento-incra \
  --nome "Projetos de Assentamento" \
  --tipo AssentamentoRural \
  --origem "INCRA — Acervo Fundiário (Assentamento Brasil)"
```

Última carga de referência: 8.215 projetos, 1 descartado por geometria inválida.

---

## Territórios quilombolas (IBGE)

```bash
curl -L -G -o quilombolas.zip "https://geoservicos.ibge.gov.br/geoserver/CGMAT/ows" \
  --data-urlencode "service=WFS" --data-urlencode "version=1.0.0" \
  --data-urlencode "request=GetFeature" \
  --data-urlencode "typeName=CGMAT:qg_2022_620_territorioquilombola__v02" \
  --data-urlencode "outputFormat=SHAPE-ZIP"
unzip quilombolas.zip

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- importar-camada \
  --arquivo qg_2022_620_territorioquilombola__v02Polygon.shp \
  --chave quilombola-ibge \
  --nome "Territórios Quilombolas" \
  --tipo TerritorioQuilombola \
  --origem "IBGE — Território Quilombola 2022" \
  --ano 2022
```

- A fonte é o **IBGE**, não o INCRA. Todas as rotas do INCRA que tentei exigem
  login: o `export_shp.py` pede credencial, a listagem de `csv_shp/zip/` devolve
  403, os nomes prováveis do arquivo dão 404, e o visualizador do Acervo
  Fundiário redireciona para `login.php` — confirmado abrindo a página num
  navegador de verdade e lendo o registro de rede.
- O arquivo vem em **3D**. O PostGIS aqui tem coluna 2D e recusava a carga
  inteira com "Geometry has Z dimension but column does not"; a preparação de
  geometria passou a achatar o Z.
- O rótulo inclui a situação: "Mel da Pedreira (TITULADO)". Território titulado
  e em processo de titulação restringem coisas diferentes.

Última carga de referência: 495 territórios, nenhum descartado.

---

## O que ainda não tem camada

Só `OUT-001`, e por definição: é a regra coringa, para perímetros restritivos
que a própria indústria definir. Sem um cadastro desses, ela fica sem base — e
o laudo continua dizendo isso em vez de tratar como atendida.

---

## Cadastro de Empregadores (MTE) — lista restritiva por documento

> **Esta é a única fonte que ainda depende de alguém.** O MTE publica em PDF, e
> lê-lo exige o `pdftotext` instalado na máquina — uma dependência externa que
> preferi não pendurar na aplicação sem combinar antes. A lista tem prazo de
> trinta dias como as outras: quando vence, aparece como vencida em `camadas`,
> o `recarregar` avisa, e a aplicação registra um aviso a cada conferência.
> Ela não envelhece em silêncio — só não se atualiza sozinha.

A "lista suja" do trabalho análogo à escravidão. Não é camada geográfica: entra
em `geo.restricao_documento` e é consultada pela regra TRB-001.

O MTE publica **só em PDF** — tentei `.csv`, `.xlsx` e `.ods` no mesmo caminho e
todos devolvem 403. Por isso a importação recebe o texto já extraído:

```bash
curl -L -o cadastro.pdf \
  "https://www.gov.br/trabalho-e-emprego/pt-br/assuntos/inspecao-do-trabalho/areas-de-atuacao/cadastro_de_empregadores.pdf"

# -table, e não -layout: ver a armadilha abaixo
pdftotext -table cadastro.pdf cadastro.txt
iconv -f ISO-8859-1 -t UTF-8 cadastro.txt > cadastro-utf8.txt

dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- \
  importar-cadastro-empregadores --arquivo cadastro-utf8.txt
```

### A armadilha do `-layout`

Com `pdftotext -layout`, o nome do empregador que quebra em duas linhas sai
intercalado com o registro seguinte: a continuação do item 35 aparece na linha
que **começa com "36"**. O importador atribuía então o CNPJ de uma empresa ao
registro de outra — num cadastro de trabalho escravo.

Foi assim que apareceu: `ALTENHOFEN (SC)` gravado como "item 36 — MG". O modo
`-table` mantém cada registro na própria linha e resolve. **Não troque de volta
para `-layout`.**

O importador relata quantos registros ficaram sem documento legível. Com
`-table` esse número é zero; se subir numa carga futura, o layout do PDF mudou
e a extração precisa ser revista antes de confiar no resultado.

Última carga de referência: 575 registros, 563 pessoas distintas, nenhum sem
documento.
