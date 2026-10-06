// Desenho de perímetros de imóvel sobre o Google Maps.
//
// Mora num arquivo só porque já teve três cópias — detalhe do imóvel, cadastro
// e painel — e o mesmo defeito estava nas três: mandavam geometria crua para o
// addGeoJson, que só aceita Feature ou FeatureCollection. Corrigir uma e
// esquecer as outras é o que vinha acontecendo, e o pior é que o que ficou
// errado passa a parecer revisado.
window.knsPerimetro = (function () {
    'use strict';

    var ESTILO = {
        strokeColor: '#2FA7F5',
        strokeOpacity: 0.8,
        strokeWeight: 2,
        fillColor: '#2FA7F5',
        fillOpacity: 0.35
    };

    // A base guarda o perímetro como geometria crua — {"type":"Polygon",...} —
    // e o addGeoJson recusa isso. A recusa não aparece: o mapa carrega,
    // centraliza no lugar certo e fica sem desenho, o que se lê como "imóvel
    // sem perímetro".
    function comoFeature(objeto) {
        if (objeto && (objeto.type === 'Feature' || objeto.type === 'FeatureCollection')) {
            return objeto;
        }

        return { type: 'Feature', geometry: objeto, properties: {} };
    }

    function limpar(map) {
        map.data.forEach(function (feicao) { map.data.remove(feicao); });
    }

    function estilizar(map) {
        map.data.setStyle(ESTILO);
    }

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
    function acrescentar(map, geoJsonTexto) {
        if (!map || !geoJsonTexto) {
            return [];
        }

        try {
            return map.data.addGeoJson(comoFeature(JSON.parse(geoJsonTexto)));
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

        /**
         * Desenha um perímetro só, substituindo o que estiver no mapa, e
         * enquadra nele. Para as telas de um imóvel.
         */
        desenhar: function (map, geoJsonTexto) {
            if (!map || !geoJsonTexto) {
                return false;
            }

            limpar(map);

            var feicoes = acrescentar(map, geoJsonTexto);

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
        }
    };
})();
