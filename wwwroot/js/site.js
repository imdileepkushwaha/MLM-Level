// MLM Level Plan - Client Interactivity Helpers

// Copy Referral Link to Clipboard
function copyReferralLink() {
    var copyText = document.getElementById("referralLink");
    if (!copyText) return;

    // Select the text field
    copyText.select();
    copyText.setSelectionRange(0, 99999); // For mobile devices

    // Copy the text inside the text field
    navigator.clipboard.writeText(copyText.value).then(function() {
        var copyBtn = document.getElementById("copyBtn");
        if (copyBtn) {
            var originalHTML = copyBtn.innerHTML;
            copyBtn.innerHTML = '<i class="bi bi-check-lg me-1"></i>Copied!';
            copyBtn.classList.remove("btn-cyber-cyan");
            copyBtn.classList.add("btn-success");
            
            setTimeout(function() {
                copyBtn.innerHTML = originalHTML;
                copyBtn.classList.remove("btn-success");
                copyBtn.classList.add("btn-cyber-cyan");
            }, 2500);
        }
    }).catch(function(err) {
        console.error('Could not copy text: ', err);
    });
}

// Fade out alert messages automatically
document.addEventListener("DOMContentLoaded", function() {
    var alerts = document.querySelectorAll(".alert-dismissible");
    alerts.forEach(function(alert) {
        setTimeout(function() {
            var bsAlert = bootstrap.Alert.getInstance(alert);
            if (bsAlert) {
                bsAlert.close();
            } else {
                alert.style.transition = "opacity 0.6s ease";
                alert.style.opacity = "0";
                setTimeout(function() {
                    alert.remove();
                }, 600);
            }
        }, 4000);
    });
});

// Toggle password visibility utility
function togglePasswordVisibility(btn) {
    const input = btn.previousElementSibling;
    const icon = btn.querySelector('i');
    if (input.type === "password") {
        input.type = "text";
        icon.className = "bi bi-eye-slash";
    } else {
        input.type = "password";
        icon.className = "bi bi-eye";
    }
}
