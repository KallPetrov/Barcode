package com.barcodeplatform.pda.ui

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.barcodeplatform.pda.BarcodePlatformApp
import com.barcodeplatform.pda.databinding.ActivityLoginBinding
import kotlinx.coroutines.launch

class LoginActivity : AppCompatActivity() {
    private lateinit var binding: ActivityLoginBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val repo = (application as BarcodePlatformApp).repository

        if (repo.isLoggedIn) {
            startActivity(Intent(this, MainActivity::class.java))
            finish()
            return
        }

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.btnLogin.setOnClickListener {
            val username = binding.inputUsername.text.toString().trim()
            val password = binding.inputPassword.text.toString()
            binding.btnLogin.isEnabled = false

            lifecycleScope.launch {
                repo.login(username, password)
                    .onSuccess {
                        startActivity(Intent(this@LoginActivity, MainActivity::class.java))
                        finish()
                    }
                    .onFailure {
                        Toast.makeText(this@LoginActivity, "Грешка при вход", Toast.LENGTH_SHORT).show()
                        binding.btnLogin.isEnabled = true
                    }
            }
        }
    }
}
