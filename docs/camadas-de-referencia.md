# Camadas de referência

As camadas contra as quais o perímetro do imóvel é cruzado. Cada regra do
protocolo examina um tipo de camada; **regra sem camada que cubra o imóvel não
é avaliada**, e o laudo diz isso explicitamente em vez de liberar por omissão.

## Abrangência: o que cada camada cobre

Toda camada declara a região que cobre, e o sistema mede o período que ela
alcança. Uma regra só se diz avaliada quando a camada dela chega **naquele
imóvel** e alcança o **período** que a regra examina.

Isso parece detalhe e não é. Antes a pergunta era "existe camada deste tipo?",
respondida uma vez para o país inteiro — e o PRODES da Amazônia respondia que
sim para uma fazenda de Goiás, onde não tem um polígono sequer.

O defeito apareceu num imóvel de Amaralina, em Goiás: a análise não encontrou
sobreposição de desmatamento, e o MapBiomas mostrava três alertas dentro do
perímetro, 17,49 ha, detectados em 2019 e 2020. Numa amostra de 36 imóveis dos
seis estados da base, **12 tinham desmatamento real dentro do perímetro e o
sistema enxergava 2**.

Eram três faltas somadas, e uma quarta que escondia as outras três:

| | O que havia | O que faltava |
|---|---|---|
| Espaço | PRODES e DETER só da Amazônia | 334 mil imóveis em GO e MS sem camada nenhuma |
| Tempo (PRODES) | só o ano de 2024 | a regra diz "a partir de 2008" |
| Tempo (alertas) | janela móvel de 12 meses | alerta de 2019 some, e ele decide compra |
| **Aviso** | **nenhum** | **as três passavam como "verificado"** |

A quarta é a que destruía a credibilidade: sem ela, as outras três apareceriam
como regra não avaliada e o analista iria atrás. Com ela, saíam como liberação.

A abrangência é **declarada no catálogo**, nunca deduzida das feições.
Deduzir seria circular: camada de desmatamento só tem polígono onde houve
desmatamento, e a ausência deles tanto pode significar "floresta intacta"
quanto "esta camada nunca olhou para cá". Só quem publica sabe qual das duas.

O período é o contrário: **medido**, nunca declarado. O menor ano presente nas
feições é o começo da cobertura, e medir fecha a porta para a declaração
divergir do arquivo.

Para ver o que está carregado e o que cada camada cobre:

```
CHAVE                      TIPO                       FEIÇÕES  ABRANGÊNCIA        DESDE  ATUALIZADA     SITUAÇÃO
prodes-cerrado             DesmatamentoConsolidado  1.566.542  regional            2008  hoje           vence em 365 dias
deter-amazonia             AlertaDesmatamento          20.910  regional            2025  hoje           vence em 7 dias
embargo-ibama              EmbargoAmbiental            57.953  nacional               —  hoje           vence em 7 dias

NUNCA CARREGADAS — as regras que dependem delas saem como não avaliadas:
prodes-pampa               DesmatamentoConsolidado          —  Pampa
```

A última lista é de propósito: enquanto ela não existia, a única pista de que
faltava o PRODES do Cerrado era a ausência de uma linha — e ausência de linha
não chama atenção de ninguém.

### Limites de biomas

A camada `biomas-brasil` é o pré-requisito das camadas regionais: é dela que
sai o recorte que cada uma declara cobrir. Seis polígonos, do INPE.

Vem do INPE, e não do IBGE que também os publica, por um motivo prático: assim
o recorte usado para dizer "o PRODES do Cerrado cobre aqui" é exatamente o
recorte com que o PRODES do Cerrado foi gerado. Duas fontes quase iguais
deixariam uma faixa de desacordo bem na divisa — e divisa de bioma é onde ficam
Goiás, Tocantins e Mato Grosso.

Ela não restringe imóvel nenhum e fica de fora do cruzamento. Sem isso, todo
imóvel do país passaria a ter uma "sobreposição" com o próprio bioma, do
tamanho do imóvel inteiro: nenhum veredito mudaria, mas o mapa do laudo
pintaria a fazenda toda de cor de alerta.

Carregue-a antes das regionais. Uma camada regional que não consegue resolver o
recorte **não é publicada** — gravar "nacional" ali seria exatamente a mentira
que o campo existe para impedir.

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

| Regra | Tipo de camada | Fonte | Abrangência |
|---|---|---|---|
| EMB-001 | EmbargoAmbiental | IBAMA Dados Abertos | nacional |
| DES-001 | DesmatamentoConsolidado | PRODES / TerraBrasilis | **uma camada por bioma** |
| TI-001 | TerraIndigena | FUNAI Geoserver | nacional |
| ALE-001 | AlertaDesmatamento | DETER (Amazônia, Cerrado) + MapBiomas | MapBiomas é nacional |
| UC-001 | UnidadeConservacao | CNUC/MMA | nacional |
| ASS-001 | AssentamentoRural | INCRA | nacional |
| QUI-001 | TerritorioQuilombola | IBGE | nacional |
| OUT-001 | OutroPerimetro | definido pela indústria | onde a indústria desenhou |

O PRODES é o único com uma entrada por bioma, porque o INPE publica assim. Os
seis estão no catálogo; carregar só alguns é seguro — os biomas que faltarem
fazem a DES-001 sair como **não avaliada** naqueles imóveis, em vez de liberada.

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
- **A origem é intermitente, e por isso tem reserva.** Em 01/10/2026 todo
  `GetFeature` passou a devolver 403 — com `maxFeatures`, sem ele, em `/ows`
  e em `/wfs`, tanto em SHAPE-ZIP quanto em GeoJSON — enquanto o
  `GetCapabilities` no mesmo servidor continuava respondendo 200. Dois dias
  antes a carga tinha funcionado. É bloqueio do nginx deles, não erro de
  requisição, e segue assim no dia seguinte.

  Quando a FUNAI recusa, a recarga cai sozinha para o **IBGE**
  (`CGMAT:qg_2022_610_terraindigena__v02`). A reserva cobre menos — 573
  polígonos contra 665, porque o quadro geográfico do IBGE não traz as terras em
  estudo — e a origem registrada no laudo diz de onde veio. Entre uma base um
  pouco menor e uma base de meses atrás, a menor protege mais: camada velha não
  avisa que está velha, só deixa de encontrar o que passou a existir.

  O arquivo do IBGE tem um polígono corrompido que fazia o leitor abortar a
  camada inteira. Agora a leitura pula o que não consegue interpretar e registra
  quantas vezes isso aconteceu — perder um polígono é ruim, perder 572 por causa
  dele é pior.
- O arquivo vem em SIRGAS 2000 (EPSG 4674). Para o Brasil a diferença para
  WGS 84 é centimétrica e irrelevante na escala de um imóvel rural.
- O rótulo inclui a fase (`Regularizada`, `Declarada`, `Em Estudo`...), porque
  elas restringem coisas juridicamente diferentes. **Todas são carregadas** —
  se a política da indústria quiser considerar apenas as regularizadas, isso é
  ajuste de regra, não de carga.

---

## Desmatamento consolidado (PRODES)

Uma camada por bioma, todas de 2008 em diante:

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --chave biomas-brasil
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --chave prodes-amazonia
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --chave prodes-cerrado
```

| Chave | Polígonos de 2008 em diante |
|---|---|
| `prodes-amazonia` | 802.277 |
| `prodes-cerrado` | 1.566.542 |
| `prodes-caatinga` | 1.460.282 |
| `prodes-mata-atlantica` | 652.415 |
| `prodes-pampa` | 145.860 |
| `prodes-pantanal` | 25.357 |

- **2008 é o corte**, e é o mesmo que a regra DES-001 declara no nome. A camada
  anterior trazia só o ano de 2024, e a regra dizia examinar desde 2008 —
  dezesseis anos que ela não tinha como encontrar, sem que nada falhasse.
- O ano de cada polígono é gravado por feição, não por camada. Era por camada
  enquanto cada carga tinha um ano só; com 2008 em diante, isso passaria a
  responder o mesmo para todo polígono e o corte da regra perderia o sentido.
- O nome da camada WFS da Amazônia difere dos outros cinco
  (`yearly_deforestation_biome` contra `yearly_deforestation`).
- Carregue `biomas-brasil` antes. Sem os limites, uma camada regional não
  consegue declarar o que cobre e a carga é recusada — de propósito.

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
- Doze meses só é defensável **porque o PRODES agora carrega de 2008 em
  diante**. Alerta é desmatamento em curso; o histórico de quem já desmatou vem
  da outra camada. Enquanto o PRODES tinha apenas 2024, essa janela era venda a
  descoberto — e foi assim que desmatamento de 2019 ficou invisível.
- São duas camadas, `deter-amazonia` e `deter-cerrado`. A do Cerrado existe
  desde sempre e nunca tinha sido carregada: Goiás, Tocantins e Mato Grosso do
  Sul não têm outro aviso de desmatamento em curso além dela.

Última carga de referência: 20.910 alertas na Amazônia, 23.603 no Cerrado.

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
| MapBiomas Alerta | diária | semanal |
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

Recarga desassistida só é segura por causa de quatro coisas que vêm antes dela.

**Conferência contra a origem.** Antes de baixar, o sistema pergunta à origem
quantas feições ela tem (`resultType=hits`, o mesmo recorte do download). No
fim, compara. Diferença maior que 1% recusa a carga.

Existe por causa do defeito mais caro desta base. O GeoServer do INPE limita
cada requisição a 50.000 feições — `CountDefault` no GetCapabilities — e, acima
disso, devolve as primeiras 50.000 com **200 OK**. Nada falha: o zip é válido,
o shapefile abre, o importador grava, a troca versionada publica. O PRODES da
Amazônia entrou no ar com 50.000 polígonos de 802.277, e não houve um registro
de erro em lugar nenhum.

A conferência de encolhimento, logo abaixo, não pegaria: ela compara com a
versão anterior, e na primeira carga não existe anterior. Era cega exatamente
onde precisava enxergar.

Quando a conferência não pode ser feita — origem que não é WFS, servidor que
não aceita `resultType=hits` — a carga entra, e o registro diz que entrou sem
rede. A primeira versão desta sonda tinha um erro de uma letra na expressão que
lê a contagem: nunca casava, devolvia nulo, e a carga seguia sem conferência.
Como nada falhava, só apareceu porque alguém estava lendo o registro na hora.
Uma conferência que se desliga sozinha sem avisar não é conferência.

**Download paginado, com repetição por página.** As camadas que passam do teto
do servidor são baixadas em páginas de 50.000, com `sortBy` num campo estável
(`fid`, no PRODES) e `startIndex`. Cada página é importada e apagada em
seguida, então o disco nunca guarda mais que uma — medido na Mata Atlântica,
145 MB em vez de três gigabytes. A publicação acontece uma vez, no fim:
publicar página a página deixaria a análise rodando contra uma fração da camada
entre uma e outra.

A requisição que falha por motivo passageiro é repetida, com espera de 15s, 45s
e dois minutos. Insiste em 5xx, em 429 e em falha de rede; não insiste em 4xx,
porque pedido errado continua errado na terceira vez e repetir só adia a
mensagem que explica o erro.

Isso não é zelo preventivo. O PRODES do Cerrado são trinta e duas páginas: na
primeira tentativa a décima nona voltou 504 e levou junto as dezoito já
baixadas. Na segunda, o mesmo servidor devolveu 502 e 504 em cinco momentos
diferentes e a carga fechou inteira. Quanto maior a camada, mais requisições e
maior a chance de uma pegar o servidor num mau momento — e as camadas que mais
importam são justamente as maiores.

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

## MapBiomas Alerta

```bash
dotnet run --project tools/GeotecnologiaKNS.Geo.Cli -- recarregar --chave mapbiomas-alerta
```

Complementa o DETER em vez de repeti-lo: o DETER cobre a Amazônia, o MapBiomas
Alerta cobre **todos os biomas** e já vem validado contra imagem de alta
resolução. Quando os dois apontam a mesma área, o motor agrupa os achados sob a
regra ALE-001 e o laudo cita as duas origens — ninguém é bloqueado duas vezes
pelo mesmo fato.

- **Histórico inteiro, não doze meses.** A janela móvel era o defeito mais caro
  do catálogo. Doze meses responde "foi desmatado recentemente?"; quem compra
  boi pergunta outra coisa — "esta terra foi desmatada depois do corte de
  2008?" — e para essa pergunta um alerta de 2019 vale tanto quanto o de ontem.
  São 541 mil alertas.
- `detected_at` é texto no formato `AAAA-MM-DD`, então a comparação por data
  funciona como comparação de texto.
- Os nomes dos campos chegam truncados em dez caracteres no shapefile, que é o
  limite de coluna do DBF: `detected_at` vira `detected_a`. O filtro vai para o
  servidor, que conhece o nome inteiro; quem lê os atributos gravados precisa
  usar o truncado.
- O WFS é público: não exige o cadastro que a plataforma pede para o restante.
- O alerta não tem nome, então o rótulo usa o município (`cities`).

Última carga de referência: 19.400 alertas em 37s, 26,6 MB.

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

- **É a única origem que não declara a codificação.** O zip do INCRA vem sem
  `.cpg` nem `.cst`, e a biblioteca assume UTF-8 quando não há declaração —
  mas o arquivo é Latin1. O resultado ia para o laudo: "PA PROVÍNCIA" saía com
  um caractere de substituição no meio do nome.

  A leitura passa a olhar o cabeçalho do DBF (byte 29, o *language driver id*;
  o INCRA declara 0x57, que é ANSI) e, na falta de tudo, assume Latin1. Todas as
  demais origens vêm de GeoServer, que sempre escreve `.cst` com ISO-8859-1 —
  essas continuam pelo caminho de antes.

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
