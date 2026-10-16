// Prende o foco de teclado dentro do modal enquanto ele estiver aberto, para quem navega
// sem mouse nunca cair sem querer no conteudo por tras. Ao fechar, devolve o foco para o
// elemento que abriu o modal. Sem biblioteca: so Tab e Shift+Tab.
export function prenderFoco(elementoModal) {
    const anterior = document.activeElement;
    const raiz = document.documentElement;

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

    elementoModal.addEventListener('keydown', aoTeclar);
    raiz.classList.add('modal-aberto');

    // Foca a propria folha, nao o primeiro campo: no Android o foco num campo abre o teclado
    // sem ninguem pedir. O leitor de tela anuncia o titulo, e o Tab segue para o primeiro campo.
    elementoModal.focus({ preventScroll: true });

    return {
        encerrar() {
            elementoModal.removeEventListener('keydown', aoTeclar);

            if (!document.querySelector('.fundo-modal .modal')) {
                raiz.classList.remove('modal-aberto');
            }

            if (anterior instanceof HTMLElement && document.contains(anterior)) {
                anterior.focus({ preventScroll: true });
            }
        }
    };
}
