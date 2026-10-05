// Desenho do perímetro do imóvel sobre o Google Maps.
//
// Mora num arquivo só porque já tinha duas cópias: a tela de detalhe e a de
// cadastro desenhavam o mesmo polígono com código parecido, e o defeito abaixo
// existia nas duas. Corrigir uma e esquecer a outra é o tipo de coisa que passa
// despercebida justamente porque "já foi corrigido".
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
    // e o addGeoJson do Google só aceita Feature ou FeatureCollection. Geometria
    // solta ele recusa, e a recusa não aparece: o mapa carrega, centraliza no
    // imóvel certo e fica sem desenho, o que se lê como "imóvel sem perímetro".
    function comoFeature(objeto) {
        if (objeto && (objeto.type === 'Feature' || objeto.type === 'FeatureCollection')) {
            return objeto;
        }

        return { type: 'Feature', geometry: objeto, properties: {} };
    }

    function limpar(map) {
        map.data.forEach(function (feicao) { map.data.remove(feicao); });
    }

    // Enquadra o imóvel inteiro. Zoom fixo corta fazenda grande, e o pedaço que
    // fica de fora é tão capaz de ter restrição quanto o que fica dentro.
    function enquadrar(map, feicoes) {
        var limites = new google.maps.LatLngBounds();
        var pontos = 0;

        feicoes.forEach(function (feicao) {
            feicao.getGeometry().forEachLatLng(function (ponto) {
                limites.extend(ponto);
                pontos++;
            });
        });

        if (pontos > 0) {
            map.fitBounds(limites);
        }

        return pontos;
    }

    return {
        /**
         * Desenha o perímetro e enquadra o mapa nele.
         * Devolve true quando algo foi de fato desenhado.
         */
        desenhar: function (map, geoJsonTexto) {
            if (!map || !geoJsonTexto) {
                return false;
            }

            limpar(map);

            var feicoes;

            try {
                feicoes = map.data.addGeoJson(comoFeature(JSON.parse(geoJsonTexto)));
            } catch (erro) {
                // Sem isto o mapa fica em branco e parece imóvel sem perímetro,
                // quando o que houve foi perímetro ilegível. Eram justamente os
                // dois casos que precisavam ser distinguidos.
                console.error('Perímetro não pôde ser desenhado:', erro);
                return false;
            }

            map.data.setStyle(ESTILO);

            return enquadrar(map, feicoes) > 0;
        }
    };
})();
