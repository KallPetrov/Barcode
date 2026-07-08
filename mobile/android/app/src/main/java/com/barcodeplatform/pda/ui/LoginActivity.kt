package com.barcodeplatform.pda.ui

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.widget.EditText
import android.widget.Toast
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import com.barcodeplatform.pda.CalacApp
import com.barcodeplatform.pda.R
import com.barcodeplatform.pda.databinding.ActivityLoginBinding
import kotlinx.coroutines.launch

class LoginActivity : AppCompatActivity() {
    private lateinit var binding: ActivityLoginBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val repo = (application as CalacApp).repository

        if (repo.isLoggedIn) {
            startActivity(Intent(this, MainActivity::class.java))
            finish()
            return
        }

        binding = ActivityLoginBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.btnSettings.setOnClickListener {
            showSettingsDialog(repo)
        }

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

    private fun showSettingsDialog(repo: com.barcodeplatform.pda.data.repository.PlatformRepository) {
        val view = LayoutInflater.from(this).inflate(R.layout.dialog_settings, null)
        val input = view.findViewById<EditText>(R.id.inputApiUrl)
        input.setText(repo.apiUrl)

        AlertDialog.Builder(this)
            .setTitle("Настройки на сървъра")
            .setView(view)
            .setPositiveButton("Запази") { _, _ ->
                val newUrl = input.text.toString().trim()
                if (newUrl.isNotEmpty()) {
                    repo.updateApiUrl(newUrl)
                    Toast.makeText(this, "Адресът е обновен", Toast.LENGTH_SHORT).show()
                }
            }
            .setNegativeButton("Отказ", null)
            .show()
    }
}
