// Cadastro de propriedade por número do CAR.
// O desenho manual de polígono foi descontinuado: o perímetro vem da base pública.

let mapaCar = null;
let camadasCar = [];

$(document).ready(function () {
    $('#btn-consultar-car').on('click', consultarCar);

    $('#CodigoCar').on('keydown', function (event) {
        if (event.key === 'Enter') {
            event.preventDefault();
            consultarCar();
        }
    });

    // Trocar o CAR invalida o que foi consultado antes.
    $('#CodigoCar').on('input', function () {
        $('#car-resultado').addClass('d-none');
        $('#btn-cadastrar').prop('disabled', true);
    });
});

function consultarCar() {
    const codigoCar = $('#CodigoCar').val();

    if (!codigoCar) {
        exibirMensagem('warning', 'Informe o número do CAR.');
        return;
    }

    const botao = $('#btn-consultar-car');
    botao.prop('disabled', true);
    exibirMensagem('info', 'Consultando a base do CAR...');

    $.ajax({
        url: '/Propriedades/ConsultarCar',
        type: 'GET',
        data: { codigoCar: codigoCar },
        dataType: 'json'
    })
        .done(function (resposta) {
            if (!resposta.sucesso) {
                exibirMensagem('warning', resposta.mensagem);
                $('#car-resultado').addClass('d-none');
                $('#btn-cadastrar').prop('disabled', true);
                return;
            }

            preencherResultado(resposta);
        })
        .fail(function () {
            exibirMensagem('danger', 'Não foi possível consultar a base do CAR.');
        })
        .always(function () {
            botao.prop('disabled', false);
        });
}

function preencherResultado(imovel) {
    exibirMensagem('success', imovel.mensagem);

    $('#CodigoCar').val(imovel.codigoCar);
    $('#car-municipio').text(formatarTexto(imovel.municipio) + ' / ' + formatarTexto(imovel.uf));
    $('#car-situacao').text(formatarTexto(imovel.situacao));
    $('#car-centro').text(imovel.centroLat.toFixed(6) + ', ' + imovel.centroLng.toFixed(6));

    const area = imovel.areaCalculadaHa || imovel.areaHa;
    $('#car-area').text(area ? area.toLocaleString('pt-BR', { maximumFractionDigits: 2 }) : '-');

    const carregadaEm = imovel.baseCarregadaEm
        ? new Date(imovel.baseCarregadaEm).toLocaleDateString('pt-BR')
        : 'data não registrada';
    $('#car-origem').text(formatarTexto(imovel.origem) + ' — carga de ' + carregadaEm);

    $('#car-resultado').removeClass('d-none');
    $('#btn-cadastrar').prop('disabled', false);

    desenharPerimetro(imovel);
}

function desenharPerimetro(imovel) {
    if (!window.googleMapsApiKey || !document.getElementById('map')) {
        return;
    }

    window.initMapCar = function () {
        const centro = { lat: imovel.centroLat, lng: imovel.centroLng };

        if (mapaCar === null) {
            mapaCar = new google.maps.Map(document.getElementById('map'), {
                zoom: 13,
                center: centro,
                disableDefaultUI: true,
                mapTypeId: 'terrain'
            });
        } else {
            mapaCar.setCenter(centro);
        }

        camadasCar.forEach(function (camada) { camada.setMap(null); });
        camadasCar = [];

        mapaCar.data.forEach(function (feature) { mapaCar.data.remove(feature); });
        mapaCar.data.addGeoJson(JSON.parse(imovel.perimetro));
        mapaCar.data.setStyle({
            strokeColor: '#00b347',
            strokeOpacity: 0.8,
            strokeWeight: 2,
            fillColor: '#00b347',
            fillOpacity: 0.35
        });
    };

    if (window.google && window.google.maps) {
        window.initMapCar();
        return;
    }

    if (document.getElementById('google-maps-script')) {
        return;
    }

    const script = document.createElement('script');
    script.id = 'google-maps-script';
    script.src = 'https://maps.googleapis.com/maps/api/js?key=' +
        encodeURIComponent(window.googleMapsApiKey) + '&callback=initMapCar&v=weekly';
    script.defer = true;
    document.head.appendChild(script);
}

function exibirMensagem(tipo, texto) {
    $('#car-mensagem').html('<div class="alert alert-' + tipo + '">' + texto + '</div>');
}

function formatarTexto(valor) {
    return valor ? valor : '-';
}
