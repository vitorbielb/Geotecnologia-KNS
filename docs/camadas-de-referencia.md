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
| QUI-001 | TerritorioQuilombola | INCRA | **pendente** |
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

## Periodicidade sugerida

| Camada | Frequência da fonte | Recarga sugerida |
|---|---|---|
| Embargos IBAMA | diária | semanal |
| DETER | quase diária | semanal |
| PRODES | anual | anual |
| Terras indígenas | esporádica | trimestral |

A recarga substitui a camada inteira: feição que saiu da fonte desaparece do
sistema, e nada é duplicado. Laudos já emitidos não mudam — eles guardam o
retrato das camadas e das regras usadas na execução.

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

## Territórios quilombolas — ainda sem rota

Única regra do protocolo que continua sem camada, e é de severidade BLOQUEIO.
O que foi tentado e não funcionou:

- `certificacao.incra.gov.br/csv_shp/export_shp.py` exige login;
- a listagem de `csv_shp/zip/` devolve 403, e os nomes prováveis do arquivo
  (`Quilombola Brasil.zip` e variações) devolvem 404 — diferente do
  `Assentamento Brasil.zip`, que existe;
- `acervofundiario.incra.gov.br` não expõe WFS, i3Geo nem GeoServer nos
  caminhos usuais;
- a API do `dados.gov.br` exige chave.

Caminhos que restam: pedir o arquivo ao INCRA, obter credencial do portal de
certificação, ou inspecionar o visualizador do Acervo Fundiário com um
navegador de verdade — ele provavelmente carrega a camada por alguma chamada
que não aparece no HTML inicial.

Enquanto isso, o laudo diz explicitamente que QUI-001 não foi avaliada, e
nenhum imóvel é liberado sem essa verificação.
