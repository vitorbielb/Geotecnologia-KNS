// Desenho de perímetros de imóvel sobre o Google Maps.
//
// Mora num arquivo só porque já teve três cópias — detalhe do imóvel, cadastro
// e painel — e o mesmo defeito estava nas três: mandavam geometria crua para o
// addGeoJson, que só aceita Feature ou FeatureCollection. Corrigir uma e
// esquecer as outras é o que vinha acontecendo, e o pior é que o que ficou
// errado passa a parecer revisado.
window.knsPerimetro = (function () {
    'use strict';

    // A cor do polígono é o veredito da análise, lida das mesmas variáveis que
    // pintam os selos de status nas listas. O mapa e a tabela não podem dizer
    // cores diferentes sobre o mesmo imóvel.
    var PALETA = {
        Liberado: { variavel: '--kns-liberado', padrao: '#1F9D57' },
        Alerta: { variavel: '--kns-alerta', padrao: '#C98A00' },
        Bloqueado: { variavel: '--kns-bloqueado', padrao: '#C7442F' },
        Solicitado: { variavel: '--kns-azul', padrao: '#0E91EF' },

        // Imóvel que nunca foi analisado. Cinza de propósito: dar a ele a cor
        // de liberado afirmaria o que ninguém verificou.
        '': { variavel: '--kns-neutro', padrao: '#6C8394' }
    };

    // Mapas que pediram estilo por situação. Guardados para repintar quando o
    // tema trocar — a variável CSS muda, e o que já está desenhado não
    // acompanha sozinho.
    var mapasPintados = [];

    function corDaSituacao(situacao) {
        var entrada = PALETA[situacao || ''] || PALETA[''];

        return window.knsTemaComponentes
            ? window.knsTemaComponentes.cor(entrada.variavel, entrada.padrao)
            : entrada.padrao;
    }

    function estiloDa(situacao) {
        var cor = corDaSituacao(situacao);

        return {
            strokeColor: cor,
            strokeOpacity: 0.9,
            strokeWeight: 2,
            fillColor: cor,
            fillOpacity: 0.35
        };
    }

    // A base guarda o perímetro como geometria crua — {"type":"Polygon",...} —
    // e o addGeoJson recusa isso. A recusa não aparece: o mapa carrega,
    // centraliza no lugar certo e fica sem desenho, o que se lê como "imóvel
    // sem perímetro".
    function comoFeature(objeto, propriedades) {
        if (objeto && (objeto.type === 'Feature' || objeto.type === 'FeatureCollection')) {
            return objeto;
        }

        return { type: 'Feature', geometry: objeto, properties: propriedades || {} };
    }

    function limpar(map) {
        map.data.forEach(function (feicao) { map.data.remove(feicao); });
    }

    function aplicarEstilo(map) {
        // Por feição, e não um estilo só para o mapa inteiro: cada imóvel tem
        // a cor do próprio veredito.
        map.data.setStyle(function (feicao) {
            return estiloDa(feicao.getProperty('situacao'));
        });
    }

    function estilizar(map) {
        aplicarEstilo(map);

        if (mapasPintados.indexOf(map) < 0) {
            mapasPintados.push(map);
        }
    }

    document.addEventListener('kns:tema', function () {
        mapasPintados.forEach(function (map) {
            try { aplicarEstilo(map); } catch (e) { /* mapa descartado */ }
        });
    });

    function contornar(feicoes, limites) {
        var pontos = 0;

        feicoes.forEach(function (feicao) {
            feicao.getGeometry().forEachLatLng(function (ponto) {
                limites.extend(ponto);
                pontos++;
            });
        });

        return pontos;
    }

    /**
     * Acrescenta um perímetro ao mapa, sem apagar o que já está lá.
     * Devolve as feições criadas, ou lista vazia quando não deu para desenhar.
     */
    function acrescentar(map, geoJsonTexto, situacao) {
        if (!map || !geoJsonTexto) {
            return [];
        }

        try {
            return map.data.addGeoJson(
                comoFeature(JSON.parse(geoJsonTexto), { situacao: situacao || '' }));
        } catch (erro) {
            // Devolver vazio em vez de propagar é o que permite ao painel
            // continuar desenhando as outras propriedades: um perímetro
            // ilegível derrubava o laço inteiro e o mapa ficava vazio.
            console.error('Perímetro não pôde ser desenhado:', erro);
            return [];
        }
    }

    return {
        acrescentar: acrescentar,
        estilizar: estilizar,
        corDaSituacao: corDaSituacao,

        /**
         * Desenha um perímetro só, substituindo o que estiver no mapa, e
         * enquadra nele. Para as telas de um imóvel.
         */
        desenhar: function (map, geoJsonTexto, situacao) {
            if (!map || !geoJsonTexto) {
                return false;
            }

            limpar(map);

            var feicoes = acrescentar(map, geoJsonTexto, situacao);

            if (feicoes.length === 0) {
                return false;
            }

            estilizar(map);

            // Enquadra o imóvel inteiro. Zoom fixo corta fazenda grande, e o
            // pedaço que fica de fora é tão capaz de ter restrição quanto o que
            // fica dentro.
            var limites = new google.maps.LatLngBounds();

            if (contornar(feicoes, limites) > 0) {
                map.fitBounds(limites);
            }

            return true;
        },

        /**
         * Enquadra o mapa em tudo que já foi acrescentado, mais os pontos
         * avulsos informados. Para o painel, que mostra várias propriedades.
         */
        enquadrarTudo: function (map, pontosAvulsos) {
            var limites = new google.maps.LatLngBounds();
            var pontos = 0;

            map.data.forEach(function (feicao) {
                pontos += contornar([feicao], limites);
            });

            (pontosAvulsos || []).forEach(function (ponto) {
                limites.extend(ponto);
                pontos++;
            });

            if (pontos > 0) {
                map.fitBounds(limites);
            }

            return pontos > 0;
        },

        /**
         * Marcador na cor da situação, para o imóvel ser achado em zoom de
         * país, onde o polígono tem menos de um pixel.
         */
        marcador: function (situacao) {
            return {
                path: google.maps.SymbolPath.CIRCLE,
                scale: 7,
                fillColor: corDaSituacao(situacao),
                fillOpacity: 1,
                strokeColor: '#FFFFFF',
                strokeWeight: 2
            };
        }
    };
})();
