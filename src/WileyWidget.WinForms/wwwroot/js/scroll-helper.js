// Scroll Helper Module for JARVIS Chat
// Provides IntersectionObserver-based scroll detection and smooth scrolling

let scrollObserver = null;
let dotNetRef = null;

export function initializeScrollObserver(targetElement, dotNetReference) {
    dotNetRef = dotNetReference;

    // Clean up existing observer
    if (scrollObserver) {
        scrollObserver.disconnect();
    }

    // Create IntersectionObserver to detect if user is at bottom
    const container = document.getElementById('chat-messages');
    if (!container || !targetElement) {
        console.warn('Chat container or target element not found');
        return;
    }

    scrollObserver = new IntersectionObserver(
        (entries) => {
            const isAtBottom = entries[0].isIntersecting;
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnScrollPositionChanged', isAtBottom);
            }
        },
        {
            root: container,
            threshold: 0.1
        }
    );

    scrollObserver.observe(targetElement);
}

export function scrollToBottom(containerId) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.warn(`Container ${containerId} not found`);
        return;
    }

    // Use smooth scroll if supported and user hasn't disabled motion
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    container.scrollTo({
        top: container.scrollHeight,
        behavior: prefersReducedMotion ? 'auto' : 'smooth'
    });
}

export function scrollToElement(element) {
    if (!element) {
        console.warn('Element reference is null');
        return;
    }

    element.scrollIntoView({
        behavior: 'smooth',
        block: 'end'
    });
}

// Cleanup on module unload
export function dispose() {
    if (scrollObserver) {
        scrollObserver.disconnect();
        scrollObserver = null;
    }
    dotNetRef = null;
}
