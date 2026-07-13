package com.example.appquanlychitieu.ui.auth;

import android.content.Intent;
import android.os.Bundle;
import android.widget.CheckBox;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.example.appquanlychitieu.MainActivity;
import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.remote.ApiError;
import com.example.appquanlychitieu.data.remote.RemoteCallback;
import com.example.appquanlychitieu.data.remote.dto.AuthResponseDto;
import com.example.appquanlychitieu.data.repository.AuthRepository;
import com.example.appquanlychitieu.util.SessionManager;
import com.example.appquanlychitieu.ui.common.EdgeToEdgeHelper;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.textfield.TextInputEditText;
import com.google.android.material.textfield.TextInputLayout;

public class LoginActivity extends AppCompatActivity {
    private TextInputEditText etEmail;
    private TextInputEditText etPassword;
    private CheckBox cbRememberMe;
    private MaterialButton btnLogin;
    private TextInputLayout layoutEmail;
    private TextInputLayout layoutPassword;
    private android.widget.ProgressBar progressAuth;
    private TextView tvRegister;
    private SessionManager sessionManager;
    private AuthRepository authRepository;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        sessionManager = new SessionManager(this);
        authRepository = new AuthRepository(this);

        if (sessionManager.isLoggedIn()) {
            if (sessionManager.isRememberMe()) {
                validateSavedSession();
            } else {
                sessionManager.logout();
                showLoginForm();
            }
        } else {
            showLoginForm();
        }
    }

    private void showLoginForm() {
        setContentView(R.layout.activity_login);
        EdgeToEdgeHelper.applySystemBars(findViewById(R.id.root_login));
        etEmail = findViewById(R.id.et_email);
        etPassword = findViewById(R.id.et_password);
        layoutEmail = findViewById(R.id.layout_email);
        layoutPassword = findViewById(R.id.layout_password);
        progressAuth = findViewById(R.id.progress_auth);
        cbRememberMe = findViewById(R.id.cb_remember_me);
        btnLogin = findViewById(R.id.btn_login);
        tvRegister = findViewById(R.id.tv_register);
        btnLogin.setOnClickListener(view -> login());
        tvRegister.setOnClickListener(view ->
                startActivity(new Intent(this, RegisterActivity.class)));
    }

    private void validateSavedSession() {
        if (sessionManager.hasAuthToken()) {
            navigateToMain();
            return;
        }
        sessionManager.logout();
        showLoginForm();
    }

    private void login() {
        String email = textOf(etEmail);
        String password = textOf(etPassword);
        if (email.isEmpty()) {
            layoutEmail.setError(getString(R.string.invalid_email));
            etEmail.requestFocus();
            return;
        }
        if (password.isEmpty()) {
            layoutPassword.setError(getString(R.string.invalid_password));
            etPassword.requestFocus();
            return;
        }
        layoutEmail.setError(null);
        layoutPassword.setError(null);

        setLoading(true);
        authRepository.login(email, password, new RemoteCallback<AuthResponseDto>() {
            @Override
            public void onSuccess(AuthResponseDto response) {
                setLoading(false);
                String remoteId = response.resolvedId();
                String resolvedEmail = response.resolvedEmail() == null
                        ? email : response.resolvedEmail();
                String resolvedName = response.resolvedName() == null
                        ? resolvedEmail : response.resolvedName();
                sessionManager.createRemoteLoginSession(
                        stableCacheUserId(remoteId == null ? resolvedEmail : remoteId),
                        remoteId,
                        resolvedName,
                        resolvedEmail,
                        cbRememberMe.isChecked(),
                        response.resolvedToken());
                Toast.makeText(LoginActivity.this, R.string.login_success, Toast.LENGTH_SHORT).show();
                navigateToMain();
            }

            @Override
            public void onError(ApiError error) {
                setLoading(false);
                layoutPassword.setError(error.getMessage());
            }
        });
    }

    private void setLoading(boolean loading) {
        btnLogin.setEnabled(!loading);
        progressAuth.setVisibility(loading ? android.view.View.VISIBLE : android.view.View.GONE);
    }

    private String textOf(TextInputEditText input) {
        return input.getText() == null ? "" : input.getText().toString().trim();
    }

    private long stableCacheUserId(String identity) {
        long hash = identity == null ? 1L : identity.hashCode();
        return Math.max(1L, Math.abs(hash));
    }

    private void navigateToMain() {
        Intent intent = new Intent(this, MainActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
        startActivity(intent);
        finish();
    }
}
