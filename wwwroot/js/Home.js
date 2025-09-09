// Dark Theme Ticket Booking Website JavaScript

document.addEventListener('DOMContentLoaded', function () {
    // Initialize all functionality
    initHeroSlider();
    initNavigation();
    initEventTabs();
    initAnimations();
    initCart();
    initNewsletter();
    initSearch();
});

// Hero Slider Functionality
function initHeroSlider() {
    const slides = document.querySelectorAll('.hero-slide');
    const dots = document.querySelectorAll('.dot');
    const prevBtn = document.querySelector('.hero-prev');
    const nextBtn = document.querySelector('.hero-next');
    let currentSlide = 0;
    let slideInterval;

    // Function to show specific slide
    function showSlide(index) {
        // Remove active class from all slides and dots
        slides.forEach(slide => slide.classList.remove('active'));
        dots.forEach(dot => dot.classList.remove('active'));

        // Add active class to current slide and dot
        slides[index].classList.add('active');
        dots[index].classList.add('active');

        currentSlide = index;
    }

    // Next slide function
    function nextSlide() {
        currentSlide = (currentSlide + 1) % slides.length;
        showSlide(currentSlide);
    }

    // Previous slide function
    function prevSlide() {
        currentSlide = (currentSlide - 1 + slides.length) % slides.length;
        showSlide(currentSlide);
    }

    // Auto-play functionality
    function startSlideShow() {
        slideInterval = setInterval(nextSlide, 5000);
    }

    function stopSlideShow() {
        clearInterval(slideInterval);
    }

    // Event listeners
    if (nextBtn) nextBtn.addEventListener('click', () => {
        stopSlideShow();
        nextSlide();
        startSlideShow();
    });

    if (prevBtn) prevBtn.addEventListener('click', () => {
        stopSlideShow();
        prevSlide();
        startSlideShow();
    });

    // Dot navigation
    dots.forEach((dot, index) => {
        dot.addEventListener('click', () => {
            stopSlideShow();
            showSlide(index);
            startSlideShow();
        });
    });

    // Pause on hover
    const heroSection = document.querySelector('.hero');
    if (heroSection) {
        heroSection.addEventListener('mouseenter', stopSlideShow);
        heroSection.addEventListener('mouseleave', startSlideShow);
    }

    // Start the slideshow
    startSlideShow();
}

// Navigation Functionality
function initNavigation() {
    const hamburger = document.querySelector('.hamburger');
    const navMenu = document.querySelector('.nav-menu');
    const navLinks = document.querySelectorAll('.nav-link');

    // Mobile menu toggle
    if (hamburger) {
        hamburger.addEventListener('click', () => {
            hamburger.classList.toggle('active');
            navMenu.classList.toggle('active');
        });
    }

    // Close mobile menu when clicking on a link
    navLinks.forEach(link => {
        link.addEventListener('click', () => {
            hamburger.classList.remove('active');
            navMenu.classList.remove('active');
        });
    });

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth'
                });
            }
        });
    });

    // Navbar background change on scroll
    window.addEventListener('scroll', () => {
        const header = document.querySelector('.header');
        if (window.scrollY > 100) {
            header.classList.add('scrolled');
        } else {
            header.classList.remove('scrolled');
        }
    });
}

// Event Tabs Functionality
function initEventTabs() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    const eventCards = document.querySelectorAll('.event-card');

    tabButtons.forEach(button => {
        button.addEventListener('click', () => {
            // Remove active class from all buttons
            tabButtons.forEach(btn => btn.classList.remove('active'));

            // Add active class to clicked button
            button.classList.add('active');

            // Get the tab type
            const tabType = button.dataset.tab;

            // Filter events based on tab (this would typically connect to backend)
            filterEvents(tabType);
        });
    });

    function filterEvents(type) {
        // This function would typically make an API call to filter events
        // For demonstration, we'll just add animation effects
        eventCards.forEach((card, index) => {
            card.style.animation = 'none';
            card.offsetHeight; // Trigger reflow
            card.style.animation = `fadeIn 0.6s ease ${index * 0.1}s both`;
        });

        // Simulate different event sets (in real app, this would be API data)
        console.log(`Filtering events by: ${type}`);
    }
}

// Cart Functionality
function initCart() {
    let cartItems = [];
    const cartIcon = document.querySelector('.cart-icon');
    const cartCount = document.querySelector('.cart-count');
    const bookNowButtons = document.querySelectorAll('.event-card .btn-primary');

    // Add to cart functionality
    bookNowButtons.forEach(button => {
        if (button.textContent.includes('Book Now')) {
            button.addEventListener('click', (e) => {
                e.preventDefault();

                const eventCard = button.closest('.event-card');
                const eventTitle = eventCard.querySelector('h3').textContent;
                const eventPrice = eventCard.querySelector('.price').textContent;
                const eventDate = eventCard.querySelector('.event-date');

                const eventData = {
                    id: Date.now(), // Simple ID generation
                    title: eventTitle,
                    price: eventPrice,
                    date: eventDate ? eventDate.textContent.replace(/\s+/g, ' ').trim() : 'TBD'
                };

                addToCart(eventData);
                showNotification('Event added to cart!', 'success');
            });
        }
    });

    function addToCart(event) {
        cartItems.push(event);
        updateCartCount();

        // Save to session storage
        try {
            sessionStorage.setItem('cartItems', JSON.stringify(cartItems));
        } catch (error) {
            console.log('Session storage not available, using memory storage');
        }
    }

    function updateCartCount() {
        if (cartCount) {
            cartCount.textContent = cartItems.length;
            cartCount.style.transform = 'scale(1.2)';
            setTimeout(() => {
                cartCount.style.transform = 'scale(1)';
            }, 200);
        }
    }

    // Load cart from session storage on page load
    function loadCart() {
        try {
            const savedCart = sessionStorage.getItem('cartItems');
            if (savedCart) {
                cartItems = JSON.parse(savedCart);
                updateCartCount();
            }
        } catch (error) {
            console.log('Could not load cart from session storage');
        }
    }

    loadCart();

    // Cart icon click handler
    if (cartIcon) {
        cartIcon.addEventListener('click', () => {
            showCartModal();
        });
    }

    function showCartModal() {
        // Create and show cart modal
        const modal = document.createElement('div');
        modal.className = 'cart-modal';
        modal.innerHTML = `
            <div class="cart-modal-content">
                <div class="cart-header">
                    <h3>Your Cart (${cartItems.length} items)</h3>
                    <button class="close-cart">&times;</button>
                </div>
                <div class="cart-items">
                    ${cartItems.length === 0 ?
                '<p class="empty-cart">Your cart is empty</p>' :
                cartItems.map(item => `
                            <div class="cart-item">
                                <h4>${item.title}</h4>
                                <p>${item.date}</p>
                                <span class="item-price">${item.price}</span>
                                <button class="remove-item" data-id="${item.id}">Remove</button>
                            </div>
                        `).join('')
            }
                </div>
                ${cartItems.length > 0 ?
                '<div class="cart-footer"><button class="btn btn-primary checkout-btn">Proceed to Checkout</button></div>' :
                ''
            }
            </div>
        `;

        document.body.appendChild(modal);

        // Add modal styles
        const modalStyles = `
            .cart-modal {
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(0, 0, 0, 0.8);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 10000;
                animation: fadeIn 0.3s ease;
            }
            .cart-modal-content {
                background: var(--bg-card);
                border-radius: 15px;
                padding: 30px;
                max-width: 500px;
                width: 90%;
                max-height: 80vh;
                overflow-y: auto;
                border: 1px solid var(--border-color);
            }
            .cart-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                margin-bottom: 20px;
                padding-bottom: 15px;
                border-bottom: 1px solid var(--border-color);
            }
            .close-cart {
                background: none;
                border: none;
                color: var(--text-primary);
                font-size: 24px;
                cursor: pointer;
            }
            .cart-item {
                padding: 15px;
                border-bottom: 1px solid var(--border-color);
                display: flex;
                justify-content: space-between;
                align-items: center;
                flex-wrap: wrap;
                gap: 10px;
            }
            .empty-cart {
                text-align: center;
                color: var(--text-secondary);
                padding: 40px;
            }
            .cart-footer {
                margin-top: 20px;
                text-align: center;
            }
            .remove-item {
                background: var(--error-color);
                color: white;
                border: none;
                padding: 5px 10px;
                border-radius: 5px;
                cursor: pointer;
                font-size: 12px;
            }
        `;

        // Add styles to head
        const styleSheet = document.createElement('style');
        styleSheet.textContent = modalStyles;
        document.head.appendChild(styleSheet);

        // Event listeners for modal
        modal.querySelector('.close-cart').addEventListener('click', () => {
            document.body.removeChild(modal);
            document.head.removeChild(styleSheet);
        });

        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                document.body.removeChild(modal);
                document.head.removeChild(styleSheet);
            }
        });

        // Remove item functionality
        modal.querySelectorAll('.remove-item').forEach(btn => {
            btn.addEventListener('click', () => {
                const itemId = parseInt(btn.dataset.id);
                cartItems = cartItems.filter(item => item.id !== itemId);
                updateCartCount();
                document.body.removeChild(modal);
                document.head.removeChild(styleSheet);
                showNotification('Item removed from cart', 'info');
            });
        });

        // Checkout functionality
        const checkoutBtn = modal.querySelector('.checkout-btn');
        if (checkoutBtn) {
            checkoutBtn.addEventListener('click', () => {
                showNotification('Redirecting to checkout...', 'success');
                // In a real app, this would redirect to checkout page
                setTimeout(() => {
                    document.body.removeChild(modal);
                    document.head.removeChild(styleSheet);
                }, 1000);
            });
        }
    }
}

// Search Functionality
function initSearch() {
    const searchInputs = document.querySelectorAll('.search-input, .hero-search input[type="text"]');
    const searchButtons = document.querySelectorAll('.search-btn, .hero-search .btn-primary');

    searchButtons.forEach((button, index) => {
        button.addEventListener('click', (e) => {
            e.preventDefault();
            const searchInput = searchInputs[index];
            if (searchInput && searchInput.value.trim()) {
                performSearch(searchInput.value.trim());
            }
        });
    });

    searchInputs.forEach(input => {
        input.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                if (input.value.trim()) {
                    performSearch(input.value.trim());
                }
            }
        });
    });

    function performSearch(query) {
        showNotification(`Searching for "${query}"...`, 'info');

        // Simulate search delay
        setTimeout(() => {
            showNotification(`Found results for "${query}"`, 'success');
            // In a real app, this would redirect to search results page
        }, 1000);

        console.log('Performing search for:', query);
    }
}

// Newsletter Functionality
function initNewsletter() {
    const newsletterForm = document.querySelector('.newsletter-form');

    if (newsletterForm) {
        newsletterForm.addEventListener('submit', (e) => {
            e.preventDefault();

            const emailInput = newsletterForm.querySelector('input[type="email"]');
            const email = emailInput.value.trim();

            if (validateEmail(email)) {
                showNotification('Successfully subscribed to newsletter!', 'success');
                emailInput.value = '';
            } else {
                showNotification('Please enter a valid email address.', 'error');
            }
        });
    }

    function validateEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }
}

// Animation Functionality
function initAnimations() {
    // Intersection Observer for scroll animations
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('fade-in');
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    // Observe elements for animation
    const animateElements = document.querySelectorAll(
        '.category-card, .event-card, .testimonial-card, .promo-banner'
    );

    animateElements.forEach(el => {
        observer.observe(el);
    });

    // Parallax effect for hero section
    window.addEventListener('scroll', () => {
        const scrolled = window.pageYOffset;
        const parallax = document.querySelector('.hero-slide.active');

        if (parallax) {
            const speed = scrolled * 0.5;
            parallax.style.transform = `translateY(${speed}px)`;
        }
    });
}

// Utility Functions
function showNotification(message, type = 'info') {
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.textContent = message;

    // Add notification styles
    const notificationStyles = `
        .notification {
            position: fixed;
            top: 100px;
            right: 20px;
            padding: 15px 20px;
            border-radius: 8px;
            color: white;
            font-weight: 500;
            z-index: 10001;
            animation: slideInRight 0.3s ease, fadeOut 0.3s ease 2.7s;
            max-width: 300px;
            word-wrap: break-word;
        }
        .notification-success {
            background: var(--success-color);
        }
        .notification-error {
            background: var(--error-color);
        }
        .notification-info {
            background: var(--primary-color);
        }
        @keyframes slideInRight {
            from {
                transform: translateX(100%);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
        @keyframes fadeOut {
            from {
                opacity: 1;
            }
            to {
                opacity: 0;
            }
        }
    `;

    // Add styles if not already added
    if (!document.querySelector('#notification-styles')) {
        const styleSheet = document.createElement('style');
        styleSheet.id = 'notification-styles';
        styleSheet.textContent = notificationStyles;
        document.head.appendChild(styleSheet);
    }

    // Add to DOM
    document.body.appendChild(notification);

    // Remove after 3 seconds
    setTimeout(() => {
        if (document.body.contains(notification)) {
            document.body.removeChild(notification);
        }
    }, 3000);
}

// Category click handlers
document.addEventListener('click', (e) => {
    if (e.target.closest('.category-card')) {
        const categoryCard = e.target.closest('.category-card');
        const categoryName = categoryCard.querySelector('h3').textContent;
        showNotification(`Browsing ${categoryName} events...`, 'info');

        // Add click animation
        categoryCard.style.transform = 'scale(0.95)';
        setTimeout(() => {
            categoryCard.style.transform = '';
        }, 150);
    }
});

// Loading state management
window.addEventListener('load', () => {
    document.body.classList.add('loaded');

    // Hide loading spinner if present
    const loader = document.querySelector('.loader');
    if (loader) {
        loader.style.display = 'none';
    }
});

// Error handling for images
document.addEventListener('error', (e) => {
    if (e.target.tagName === 'IMG') {
        e.target.src = 'data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNDAwIiBoZWlnaHQ9IjMwMCIgdmlld0JveD0iMCAwIDQwMCAzMDAiIGZpbGw9Im5vbmUiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyI+CjxyZWN0IHdpZHRoPSI0MDAiIGhlaWdodD0iMzAwIiBmaWxsPSIjMzc0MTUxIi8+Cjx0ZXh0IHg9IjIwMCIgeT0iMTUwIiBmaWxsPSIjNmI3MjgwIiB0ZXh0LWFuY2hvcj0ibWlkZGxlIiBkb21pbmFudC1iYXNlbGluZT0ibWlkZGxlIiBmb250LWZhbWlseT0iSW50ZXIiIGZvbnQtc2l6ZT0iMTQiPkltYWdlIE5vdCBGb3VuZDwvdGV4dD4KPC9zdmc+';
        e.target.alt = 'Image not found';
    }
}, true);

// Performance optimization: Lazy loading for images
function initLazyLoading() {
    const images = document.querySelectorAll('img[data-src]');

    const imageObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const img = entry.target;
                img.src = img.dataset.src;
                img.classList.remove('lazy');
                imageObserver.unobserve(img);
            }
        });
    });

    images.forEach(img => imageObserver.observe(img));
}

// Theme toggle functionality (optional feature)
function initThemeToggle() {
    const themeToggle = document.querySelector('.theme-toggle');

    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            document.body.classList.toggle('light-theme');
            const isLight = document.body.classList.contains('light-theme');
            localStorage.setItem('theme', isLight ? 'light' : 'dark');
            showNotification(`Switched to ${isLight ? 'light' : 'dark'} theme`, 'info');
        });

        // Load saved theme
        const savedTheme = localStorage.getItem('theme');
        if (savedTheme === 'light') {
            document.body.classList.add('light-theme');
        }
    }
}

// Advanced search functionality
function initAdvancedSearch() {
    const filterButtons = document.querySelectorAll('[data-filter]');
    const sortButtons = document.querySelectorAll('[data-sort]');
    const priceRange = document.querySelector('#price-range');
    const dateRange = document.querySelector('#date-range');

    filterButtons.forEach(button => {
        button.addEventListener('click', () => {
            const filterType = button.dataset.filter;
            applyFilter(filterType);

            // Update button states
            filterButtons.forEach(btn => btn.classList.remove('active'));
            button.classList.add('active');
        });
    });

    sortButtons.forEach(button => {
        button.addEventListener('click', () => {
            const sortType = button.dataset.sort;
            applySorting(sortType);

            // Update button states
            sortButtons.forEach(btn => btn.classList.remove('active'));
            button.classList.add('active');
        });
    });

    if (priceRange) {
        priceRange.addEventListener('change', () => {
            applyPriceFilter(priceRange.value);
        });
    }

    if (dateRange) {
        dateRange.addEventListener('change', () => {
            applyDateFilter(dateRange.value);
        });
    }

    function applyFilter(filterType) {
        console.log(`Applying filter: ${filterType}`);
        // In a real app, this would filter events from API
        showNotification(`Filtering by ${filterType}`, 'info');
    }

    function applySorting(sortType) {
        console.log(`Sorting by: ${sortType}`);
        // In a real app, this would sort events
        showNotification(`Sorted by ${sortType}`, 'info');
    }

    function applyPriceFilter(price) {
        console.log(`Price filter: ${price}`);
        showNotification(`Filtering by price: ${price}`, 'info');
    }

    function applyDateFilter(date) {
        console.log(`Date filter: ${date}`);
        showNotification(`Filtering by date: ${date}`, 'info');
    }
}

// Social sharing functionality
function initSocialSharing() {
    const shareButtons = document.querySelectorAll('.share-btn');

    shareButtons.forEach(button => {
        button.addEventListener('click', (e) => {
            e.preventDefault();
            const platform = button.dataset.platform;
            const eventTitle = button.closest('.event-card').querySelector('h3').textContent;
            shareEvent(platform, eventTitle);
        });
    });

    function shareEvent(platform, eventTitle) {
        const url = encodeURIComponent(window.location.href);
        const text = encodeURIComponent(`Check out this event: ${eventTitle}`);

        let shareUrl;

        switch (platform) {
            case 'facebook':
                shareUrl = `https://www.facebook.com/sharer/sharer.php?u=${url}`;
                break;
            case 'twitter':
                shareUrl = `https://twitter.com/intent/tweet?text=${text}&url=${url}`;
                break;
            case 'linkedin':
                shareUrl = `https://www.linkedin.com/sharing/share-offsite/?url=${url}`;
                break;
            case 'whatsapp':
                shareUrl = `https://wa.me/?text=${text}%20${url}`;
                break;
            default:
                console.log('Unknown platform');
                return;
        }

        // Open share window
        window.open(shareUrl, 'share', 'width=600,height=400,scrollbars=yes,resizable=yes');
        showNotification(`Sharing on ${platform}`, 'success');
    }
}

// Wishlist functionality
function initWishlist() {
    let wishlistItems = [];
    const wishlistButtons = document.querySelectorAll('.wishlist-btn');

    // Load wishlist from storage
    try {
        const savedWishlist = localStorage.getItem('wishlistItems');
        if (savedWishlist) {
            wishlistItems = JSON.parse(savedWishlist);
            updateWishlistUI();
        }
    } catch (error) {
        console.log('Could not load wishlist');
    }

    wishlistButtons.forEach(button => {
        button.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();

            const eventCard = button.closest('.event-card');
            const eventId = eventCard.dataset.eventId || Date.now().toString();
            const eventTitle = eventCard.querySelector('h3').textContent;

            if (isInWishlist(eventId)) {
                removeFromWishlist(eventId);
                showNotification('Removed from wishlist', 'info');
            } else {
                addToWishlist({
                    id: eventId,
                    title: eventTitle,
                    image: eventCard.querySelector('img').src
                });
                showNotification('Added to wishlist', 'success');
            }

            updateWishlistUI();
        });
    });

    function addToWishlist(event) {
        if (!isInWishlist(event.id)) {
            wishlistItems.push(event);
            saveWishlist();
        }
    }

    function removeFromWishlist(eventId) {
        wishlistItems = wishlistItems.filter(item => item.id !== eventId);
        saveWishlist();
    }

    function isInWishlist(eventId) {
        return wishlistItems.some(item => item.id === eventId);
    }

    function saveWishlist() {
        try {
            localStorage.setItem('wishlistItems', JSON.stringify(wishlistItems));
        } catch (error) {
            console.log('Could not save wishlist');
        }
    }

    function updateWishlistUI() {
        wishlistButtons.forEach(button => {
            const eventCard = button.closest('.event-card');
            const eventId = eventCard.dataset.eventId;

            if (isInWishlist(eventId)) {
                button.classList.add('active');
                button.innerHTML = '<i class="fas fa-heart"></i>';
            } else {
                button.classList.remove('active');
                button.innerHTML = '<i class="far fa-heart"></i>';
            }
        });

        // Update wishlist counter
        const wishlistCounter = document.querySelector('.wishlist-count');
        if (wishlistCounter) {
            wishlistCounter.textContent = wishlistItems.length;
        }
    }
}

// Keyboard navigation
function initKeyboardNavigation() {
    document.addEventListener('keydown', (e) => {
        // Escape key functionality
        if (e.key === 'Escape') {
            // Close any open modals
            const modals = document.querySelectorAll('.cart-modal, .search-modal');
            modals.forEach(modal => {
                if (modal.parentNode) {
                    modal.parentNode.removeChild(modal);
                }
            });
        }

        // Arrow key navigation for hero slider
        if (e.key === 'ArrowLeft') {
            const prevBtn = document.querySelector('.hero-prev');
            if (prevBtn) prevBtn.click();
        }

        if (e.key === 'ArrowRight') {
            const nextBtn = document.querySelector('.hero-next');
            if (nextBtn) nextBtn.click();
        }

        // Enter key for focused buttons
        if (e.key === 'Enter' && e.target.classList.contains('btn')) {
            e.target.click();
        }
    });

    // Focus management for accessibility
    const focusableElements = document.querySelectorAll(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );

    focusableElements.forEach(element => {
        element.addEventListener('focus', () => {
            element.classList.add('keyboard-focus');
        });

        element.addEventListener('blur', () => {
            element.classList.remove('keyboard-focus');
        });
    });
}

// Touch gestures for mobile
function initTouchGestures() {
    let touchStartX = 0;
    let touchStartY = 0;

    const heroSection = document.querySelector('.hero');

    if (heroSection) {
        heroSection.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
            touchStartY = e.touches[0].clientY;
        });

        heroSection.addEventListener('touchend', (e) => {
            const touchEndX = e.changedTouches[0].clientX;
            const touchEndY = e.changedTouches[0].clientY;

            const deltaX = touchEndX - touchStartX;
            const deltaY = touchEndY - touchStartY;

            // Horizontal swipe detection
            if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 50) {
                if (deltaX > 0) {
                    // Swipe right - previous slide
                    const prevBtn = document.querySelector('.hero-prev');
                    if (prevBtn) prevBtn.click();
                } else {
                    // Swipe left - next slide
                    const nextBtn = document.querySelector('.hero-next');
                    if (nextBtn) nextBtn.click();
                }
            }
        });
    }
}

// Initialize additional features when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    initLazyLoading();
    initThemeToggle();
    initAdvancedSearch();
    initSocialSharing();
    initWishlist();
    initKeyboardNavigation();
    initTouchGestures();
});

// Page visibility API for performance
document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
        // Pause animations and timers when page is not visible
        console.log('Page hidden - pausing animations');
    } else {
        // Resume when page becomes visible
        console.log('Page visible - resuming animations');
    }
});

// Service Worker registration (for PWA capabilities)
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js')
            .then(registration => {
                console.log('SW registered: ', registration);
            })
            .catch(registrationError => {
                console.log('SW registration failed: ', registrationError);
            });
    });
}

// Analytics tracking (placeholder for real analytics)
function trackEvent(eventName, parameters = {}) {
    console.log('Tracking event:', eventName, parameters);
    // In a real app, this would send data to analytics service
    // Example: gtag('event', eventName, parameters);
}

// Track important user interactions
document.addEventListener('click', (e) => {
    if (e.target.matches('.btn-primary')) {
        trackEvent('button_click', {
            button_text: e.target.textContent,
            page_location: window.location.href
        });
    }

    if (e.target.closest('.event-card')) {
        trackEvent('event_card_click', {
            event_title: e.target.closest('.event-card').querySelector('h3').textContent
        });
    }
});

// Export functions for potential external use
window.TicketHub = {
    showNotification,
    trackEvent,
    addToCart: (event) => console.log('Add to cart:', event),
    searchEvents: (query) => console.log('Search events:', query)
};