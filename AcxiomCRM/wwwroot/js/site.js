// AcxiomCRM Global JavaScript Utilities
document.addEventListener('DOMContentLoaded', function () {
    // Reusable modal confirmation for forms marked with data-confirm
    document.querySelectorAll('form[data-confirm]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            const message = form.getAttribute('data-confirm') || 'Are you sure you want to proceed?';
            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });
});
