// Prende o foco de teclado dentro do modal enquanto ele estiver aberto, para quem navega
// sem mouse nunca cair sem querer no conteudo por tras. Ao fechar, devolve o foco para o
// elemento que abriu o modal. Sem biblioteca: so Tab, Shift+Tab e Esc.
// O fundo parado enquanto o modal esta aberto e do CSS (html:has(.fundo-modal)), nao daqui.
export function prenderFoco(elementoModal, dotnet) {
    const anterior = document.activeElement;

    function focaveis() {
        return Array.from(elementoModal.querySelectorAll(
            'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), summary, [tabindex]:not([tabindex="-1"])'
        )).filter(el => el.offsetParent !== null);
    }

    function aoTeclar(evento) {
        if (evento.key !== 'Tab') {
            return;
        }

        const itens = focaveis();

        if (itens.length === 0) {
            return;
        }

        const primeiro = itens[0];
        const ultimo = itens[itens.length - 1];

        // Logo ao abrir o foco esta na propria folha, fora da lista: sem isto o Shift+Tab escaparia.
        if (document.activeElement === elementoModal) {
            evento.preventDefault();
            (evento.shiftKey ? ultimo : primeiro).focus();
        } else if (evento.shiftKey && document.activeElement === primeiro) {
            evento.preventDefault();
            ultimo.focus();
        } else if (!evento.shiftKey && document.activeElement === ultimo) {
            evento.preventDefault();
            primeiro.focus();
        }
    }

    // Esc e Tab tambem valem com o foco fora do modal. Isso acontece quando o elemento focado
    // some da tela (o botao de excluir uma linha que acabou de ser excluida): o foco cai no
    // body, e um ouvinte so no modal deixaria de fechar no Esc e soltaria o Tab para o fundo.
    function aoTeclarNoDocumento(evento) {
        const abertos = document.querySelectorAll('.fundo-modal .modal');

        if (abertos[abertos.length - 1] !== elementoModal) {
            return;
        }

        if (evento.key === 'Escape') {
            evento.preventDefault();
            dotnet.invokeMethodAsync('FecharPeloTeclado');
        } else if (evento.key === 'Tab' && !elementoModal.contains(document.activeElement)) {
            evento.preventDefault();
            (focaveis()[0] ?? elementoModal).focus();
        }
    }

    elementoModal.addEventListener('keydown', aoTeclar);
    document.addEventListener('keydown', aoTeclarNoDocumento);

    // Foca a propria folha, nao o primeiro campo: no Android o foco num campo abre o teclado
    // sem ninguem pedir. O leitor de tela anuncia o titulo, e o Tab segue para o primeiro campo.
    elementoModal.focus({ preventScroll: true });

    return {
        encerrar() {
            elementoModal.removeEventListener('keydown', aoTeclar);
            document.removeEventListener('keydown', aoTeclarNoDocumento);

            if (anterior instanceof HTMLElement && document.contains(anterior)) {
                anterior.focus({ preventScroll: true });
            }
        }
    };
}
